using GoatLab.Server.Data;
using GoatLab.Server.Services.Alerts;
using GoatLab.Server.Services.Push;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Jobs;

// Hourly Hangfire job. For each non-deleted, non-suspended tenant whose plan
// has SmartAlerts enabled: run the scanner and (if PushNotifications is also
// enabled) fan out a web-push for each fresh alert.
public class AlertScanJob
{
    private readonly GoatLabDbContext _db;
    private readonly AlertScannerService _scanner;
    private readonly PushService _push;
    private readonly ILogger<AlertScanJob> _logger;

    public AlertScanJob(
        GoatLabDbContext db,
        AlertScannerService scanner,
        PushService push,
        ILogger<AlertScanJob> logger)
    {
        _db = db;
        _scanner = scanner;
        _push = push;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // Pull tenants + their plan features upfront so we can filter to the
        // ones that have SmartAlerts on without round-tripping per tenant.
        var tenants = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.DeletedAt == null && t.SuspendedAt == null)
            .Include(t => t.Plan).ThenInclude(p => p!.Features)
            .ToListAsync(cancellationToken);

        var enabled = tenants
            .Where(t => t.Plan?.Features.Any(f => f.Feature == AppFeature.SmartAlerts && f.Enabled) == true)
            .ToList();

        _logger.LogInformation("Alert scan sweep: {Count} tenants with SmartAlerts enabled", enabled.Count);

        foreach (var tenant in enabled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var fresh = await _scanner.ScanTenantAsync(tenant.Id, cancellationToken);
                if (fresh.Count == 0) continue;

                var pushOn = tenant.Plan?.Features.Any(f => f.Feature == AppFeature.PushNotifications && f.Enabled) == true;
                if (!pushOn) continue;

                foreach (var alert in fresh)
                {
                    await _push.SendToTenantAsync(
                        tenant.Id,
                        new PushPayload(alert.Title, alert.Body ?? "", alert.DeepLink ?? "/alerts"),
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Alert scan failed for tenant {TenantId}", tenant.Id);
            }
        }
    }
}
