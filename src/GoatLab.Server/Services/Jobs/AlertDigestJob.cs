using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services.Email;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Jobs;

// Daily Hangfire job: emails each tenant owner a summary of SmartAlerts
// created in the last 24h. Skips tenants without the SmartAlerts feature, with
// AlertEmailEnabled=false, with no alerts in the window, or without a valid
// owner email. Mirrors the TrialReminderJob shape.
public class AlertDigestJob
{
    private readonly GoatLabDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppEmailSender _email;
    private readonly ILogger<AlertDigestJob> _logger;
    private readonly IConfiguration _config;

    public AlertDigestJob(
        GoatLabDbContext db,
        UserManager<ApplicationUser> userManager,
        IAppEmailSender email,
        ILogger<AlertDigestJob> logger,
        IConfiguration config)
    {
        _db = db;
        _userManager = userManager;
        _email = email;
        _logger = logger;
        _config = config;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var since = now.AddHours(-24);

        var tenants = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.DeletedAt == null
                     && t.SuspendedAt == null
                     && t.AlertEmailEnabled)
            .Include(t => t.Plan).ThenInclude(p => p!.Features)
            .ToListAsync(cancellationToken);

        var enabled = tenants
            .Where(t => t.Plan?.Features.Any(f => f.Feature == AppFeature.SmartAlerts && f.Enabled) == true)
            .ToList();

        _logger.LogInformation("Alert digest sweep: {Count} candidate tenants", enabled.Count);

        var publicUrl = _config.GetValue<string>("App:PublicUrl") ?? "";
        var alertsUrl = string.IsNullOrEmpty(publicUrl) ? "/alerts" : $"{publicUrl.TrimEnd('/')}/alerts";

        foreach (var tenant in enabled)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var alerts = await _db.Alerts
                .IgnoreQueryFilters()
                .Where(a => a.TenantId == tenant.Id && a.CreatedAt >= since)
                .OrderByDescending(a => a.Severity)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync(cancellationToken);
            if (alerts.Count == 0) continue;

            var ownerUserId = await _db.TenantMembers
                .IgnoreQueryFilters()
                .Where(m => m.TenantId == tenant.Id && m.Role == TenantRole.Owner)
                .OrderBy(m => m.JoinedAt)
                .Select(m => m.UserId)
                .FirstOrDefaultAsync(cancellationToken);
            if (ownerUserId is null) continue;

            var user = await _userManager.FindByIdAsync(ownerUserId);
            if (user?.Email is null || user.DeletedAt is not null) continue;

            var rows = alerts
                .Select(a => (a.Title, a.Body, Severity: a.Severity.ToString()))
                .ToList();
            var tpl = EmailTemplates.AlertDigest(user.DisplayName, tenant.Name, rows, alertsUrl);

            try
            {
                await _email.SendAsync(user.Email, tpl.Subject, tpl.Html, tpl.Text, cancellationToken);
                _logger.LogInformation("Alert digest sent: tenant {TenantId} user {UserId} alerts {Count}",
                    tenant.Id, ownerUserId, alerts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Alert digest failed for tenant {TenantId}", tenant.Id);
            }
        }
    }
}
