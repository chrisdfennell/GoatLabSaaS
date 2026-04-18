using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GoatLab.Server.Data;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Webhooks;

// Outbound webhook sender. DispatchAsync is called from controllers after a
// successful write. It fans the event out to every active Webhook in the
// current tenant whose Events column includes the event type, records a
// WebhookDelivery row, and POSTs the signed payload. Failures schedule a
// retry via WebhookRetryJob.
public class WebhookDispatcher
{
    // Backoff schedule. Index = AttemptCount of the row being retried.
    // First retry 1 minute after the failed first attempt; then 5m, then 30m.
    private static readonly TimeSpan[] Backoff =
    {
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
    };

    public const int MaxAttempts = 3;

    private readonly GoatLabDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(GoatLabDbContext db, IHttpClientFactory httpFactory, ILogger<WebhookDispatcher> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task DispatchAsync(string eventType, object payload, CancellationToken ct = default)
    {
        // Filter to tenant webhooks subscribed to this event. Tenant filter is
        // automatic via GoatLabDbContext.
        var candidates = await _db.Webhooks
            .Where(w => w.IsActive)
            .ToListAsync(ct);

        candidates = candidates
            .Where(w => SubscriptionIncludes(w.Events, eventType))
            .ToList();

        if (candidates.Count == 0) return;

        var json = JsonSerializer.Serialize(new
        {
            @event = eventType,
            deliveryId = Guid.NewGuid().ToString(),
            occurredAt = DateTime.UtcNow,
            data = payload,
        });

        foreach (var webhook in candidates)
        {
            var delivery = new WebhookDelivery
            {
                TenantId = webhook.TenantId,
                WebhookId = webhook.Id,
                EventType = eventType,
                Payload = json,
                CreatedAt = DateTime.UtcNow,
            };
            _db.WebhookDeliveries.Add(delivery);
            await _db.SaveChangesAsync(ct);

            try
            {
                await SendOneAsync(webhook, delivery, ct);
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Webhook dispatch failed for {WebhookId} event {Event}", webhook.Id, eventType);
            }
        }
    }

    // Single attempt; called by DispatchAsync and by the retry job. Caller is
    // responsible for SaveChangesAsync after this returns.
    public async Task SendOneAsync(Webhook webhook, WebhookDelivery delivery, CancellationToken ct)
    {
        delivery.AttemptCount++;
        var client = _httpFactory.CreateClient("webhooks");
        client.Timeout = TimeSpan.FromSeconds(10);

        using var req = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
        {
            Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-GoatLab-Event", delivery.EventType);
        req.Headers.Add("X-GoatLab-Delivery", delivery.DeliveryId);
        req.Headers.Add("X-GoatLab-Signature", $"sha256={ComputeSignature(delivery.Payload, webhook.Secret)}");
        req.Headers.Add("User-Agent", "GoatLab-Webhooks/1.0");

        try
        {
            using var resp = await client.SendAsync(req, ct);
            delivery.StatusCode = (int)resp.StatusCode;
            var body = await resp.Content.ReadAsStringAsync(ct);
            delivery.ResponseBody = body.Length > 500 ? body[..500] : body;

            webhook.UpdatedAt = DateTime.UtcNow;
            webhook.LastStatusCode = delivery.StatusCode;
            webhook.LastDeliveredAt = DateTime.UtcNow;

            if (resp.IsSuccessStatusCode)
            {
                delivery.DeliveredAt = DateTime.UtcNow;
                delivery.NextRetryAt = null;
                delivery.Error = null;
                webhook.LastError = null;
            }
            else
            {
                ScheduleRetry(delivery);
                delivery.Error = $"HTTP {delivery.StatusCode}";
                webhook.LastError = delivery.Error;
            }
        }
        catch (Exception ex)
        {
            delivery.StatusCode = null;
            delivery.Error = Truncate(ex.Message, 1000);
            ScheduleRetry(delivery);
            webhook.UpdatedAt = DateTime.UtcNow;
            webhook.LastError = delivery.Error;
        }
    }

    private static void ScheduleRetry(WebhookDelivery delivery)
    {
        if (delivery.AttemptCount >= MaxAttempts)
        {
            delivery.NextRetryAt = null;
            return;
        }
        // AttemptCount is now post-increment (1 on first failure). Use
        // AttemptCount-1 as the backoff index to pick 1m → 5m → 30m.
        var idx = Math.Clamp(delivery.AttemptCount - 1, 0, Backoff.Length - 1);
        delivery.NextRetryAt = DateTime.UtcNow.Add(Backoff[idx]);
    }

    public static string ComputeSignature(string payload, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(key, bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool SubscriptionIncludes(string events, string target)
    {
        if (string.IsNullOrWhiteSpace(events)) return false;
        foreach (var part in events.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(part, target, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
