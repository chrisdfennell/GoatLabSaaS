using System.Net;
using System.Text.Json;
using GoatLab.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebPush;
using WPSubscription = WebPush.PushSubscription;

namespace GoatLab.Server.Services.Push;

public record PushPayload(string Title, string Body, string Url, string? Tag = null);

// Wraps WebPush. No-op (logs and exits) when VAPID keys aren't configured so
// the broader app keeps working in dev environments without a keypair.
// Auto-prunes subscriptions that come back 404/410 — those endpoints have been
// permanently invalidated by the browser/OS push service.
public class PushService
{
    private readonly GoatLabDbContext _db;
    private readonly PushOptions _options;
    private readonly ILogger<PushService> _logger;
    private readonly WebPushClient? _client;
    private readonly VapidDetails? _vapid;

    public PushService(GoatLabDbContext db, IOptions<PushOptions> options, ILogger<PushService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;

        if (IsConfigured)
        {
            _client = new WebPushClient();
            _vapid = new VapidDetails(
                _options.Subject ?? "mailto:admin@goatlab.local",
                _options.VapidPublicKey!,
                _options.VapidPrivateKey!);
        }
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.VapidPublicKey)
        && !string.IsNullOrWhiteSpace(_options.VapidPrivateKey);

    public string? PublicKey => _options.VapidPublicKey;

    /// <summary>Send to every device subscribed by users in the tenant.</summary>
    public async Task SendToTenantAsync(int tenantId, PushPayload payload, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return;
        var subs = await _db.PushSubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        await SendToManyAsync(subs, payload, cancellationToken);
    }

    /// <summary>Send to every device subscribed by a specific user.</summary>
    public async Task SendToUserAsync(string userId, PushPayload payload, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return;
        var subs = await _db.PushSubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);
        await SendToManyAsync(subs, payload, cancellationToken);
    }

    private async Task SendToManyAsync(
        List<Shared.Models.PushSubscription> subs,
        PushPayload payload,
        CancellationToken cancellationToken)
    {
        if (subs.Count == 0) return;
        var json = JsonSerializer.Serialize(payload);
        var stale = new List<Shared.Models.PushSubscription>();

        foreach (var s in subs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _client!.SendNotificationAsync(
                    new WPSubscription(s.Endpoint, s.P256dh, s.Auth),
                    json,
                    _vapid);
                s.LastUsedAt = DateTime.UtcNow;
            }
            catch (WebPushException wex) when (
                wex.StatusCode == HttpStatusCode.Gone ||
                wex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Pruning stale push subscription {Id} ({Status})", s.Id, wex.StatusCode);
                stale.Add(s);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Push send failed for subscription {Id}", s.Id);
            }
        }

        if (stale.Count > 0)
            _db.PushSubscriptions.RemoveRange(stale);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
