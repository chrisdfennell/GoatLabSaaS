using GoatLab.Server.Data;
using GoatLab.Server.Services.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Jobs;

// Hangfire recurring job. Sweeps WebhookDelivery rows whose first attempt
// failed and whose backoff window has elapsed, and re-tries them. Cross-tenant
// (IgnoreQueryFilters) since the job runs without a request scope.
public class WebhookRetryJob
{
    private readonly GoatLabDbContext _db;
    private readonly WebhookDispatcher _dispatcher;
    private readonly ILogger<WebhookRetryJob> _logger;

    public WebhookRetryJob(GoatLabDbContext db, WebhookDispatcher dispatcher, ILogger<WebhookRetryJob> logger)
    {
        _db = db;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var pending = await _db.WebhookDeliveries
            .IgnoreQueryFilters()
            .Include(d => d.Webhook)
            .Where(d => d.DeliveredAt == null
                        && d.AttemptCount < WebhookDispatcher.MaxAttempts
                        && d.NextRetryAt != null
                        && d.NextRetryAt <= now)
            .OrderBy(d => d.NextRetryAt)
            .Take(100) // cap the batch so a huge backlog doesn't starve other jobs
            .ToListAsync(cancellationToken);

        if (pending.Count == 0) return;

        _logger.LogInformation("Webhook retry sweep: {Count} deliveries to replay", pending.Count);

        foreach (var delivery in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (delivery.Webhook is null || !delivery.Webhook.IsActive)
            {
                // Webhook was disabled/removed between attempts — abandon.
                delivery.NextRetryAt = null;
                continue;
            }

            try
            {
                await _dispatcher.SendOneAsync(delivery.Webhook, delivery, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Retry failed for delivery {DeliveryId}", delivery.Id);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
