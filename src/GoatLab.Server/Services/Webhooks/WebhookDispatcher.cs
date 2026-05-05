using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GoatLab.Server.Data;
using GoatLab.Server.Services;
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
    private readonly ITenantContext _tenantContext;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(
        GoatLabDbContext db,
        ITenantContext tenantContext,
        IHttpClientFactory httpFactory,
        ILogger<WebhookDispatcher> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    // Default overload: fires to the current tenant (query filter scopes _db.Webhooks).
    public Task DispatchAsync(string eventType, object payload, CancellationToken ct = default)
        => DispatchInternalAsync(eventType, payload, specificTenantId: null, ct);

    // Explicit-tenant overload: used by cross-tenant flows like transfers where
    // we need to notify both the seller and the buyer's webhooks. Bypasses the
    // query filter and filters by TenantId manually.
    public Task DispatchToTenantAsync(int tenantId, string eventType, object payload, CancellationToken ct = default)
        => DispatchInternalAsync(eventType, payload, specificTenantId: tenantId, ct);

    private async Task DispatchInternalAsync(string eventType, object payload, int? specificTenantId, CancellationToken ct)
    {
        List<Webhook> candidates;
        if (specificTenantId is int tid)
        {
            var bypassWas = _tenantContext.BypassFilter;
            _tenantContext.BypassFilter = true;
            try
            {
                candidates = await _db.Webhooks.IgnoreQueryFilters()
                    .Where(w => w.IsActive && w.TenantId == tid)
                    .ToListAsync(ct);
            }
            finally { _tenantContext.BypassFilter = bypassWas; }
        }
        else
        {
            candidates = await _db.Webhooks
                .Where(w => w.IsActive)
                .ToListAsync(ct);
        }

        candidates = candidates
            .Where(w => SubscriptionIncludes(w.Events, eventType))
            .ToList();

        if (candidates.Count == 0) return;

        var occurredAt = DateTime.UtcNow;

        foreach (var webhook in candidates)
        {
            // Build the payload PER delivery so the embedded deliveryId
            // matches the X-GoatLab-Delivery header — receivers should be
            // able to dedupe on either field. Previously the JSON used a
            // fresh GUID per dispatch fan-out, which differed from the
            // per-row DeliveryId we sent in the header.
            var delivery = new WebhookDelivery
            {
                TenantId = webhook.TenantId,
                WebhookId = webhook.Id,
                EventType = eventType,
                CreatedAt = occurredAt,
            };
            delivery.Payload = JsonSerializer.Serialize(new
            {
                @event = eventType,
                deliveryId = delivery.DeliveryId,
                occurredAt,
                data = payload,
            });
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

        // Original signature is HMAC over the raw body — kept as-is for
        // backward compatibility with receivers already verifying it.
        var bodySig = ComputeSignature(delivery.Payload, webhook.Secret);

        // Replay-resistant signature: HMAC over `{ts}.{payload}`, sent in a
        // separate header so receivers can opt in to freshness checks. The
        // timestamp is covered by the MAC, so an attacker can't extend the
        // valid window by replacing it. Format mirrors Stripe's.
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var tsSig = ComputeSignature($"{ts}.{delivery.Payload}", webhook.Secret);

        using var req = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
        {
            Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-GoatLab-Event", delivery.EventType);
        req.Headers.Add("X-GoatLab-Delivery", delivery.DeliveryId);
        req.Headers.Add("X-GoatLab-Signature", $"sha256={bodySig}");
        req.Headers.Add("X-GoatLab-Timestamp", ts.ToString());
        req.Headers.Add("X-GoatLab-Signature-V2", $"t={ts},v1={tsSig}");
        req.Headers.Add("User-Agent", "GoatLab-Webhooks/1.0");

        try
        {
            // ResponseHeadersRead so we don't buffer the body just to throw
            // most of it away. We then read at most ResponseBodyMaxBytes —
            // a misbehaving receiver returning gigabytes can't OOM us.
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            delivery.StatusCode = (int)resp.StatusCode;
            delivery.ResponseBody = await ReadBoundedBodyAsync(resp, ct);

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
            else if (IsRetryable((int)resp.StatusCode))
            {
                ScheduleRetry(delivery);
                delivery.Error = $"HTTP {delivery.StatusCode}";
                webhook.LastError = delivery.Error;
            }
            else
            {
                // Permanent client error (401/403/404/410/422). Retrying won't
                // change the receiver's mind — abandon and surface so the
                // tenant sees it on the deliveries page instead of silent rot.
                delivery.NextRetryAt = null;
                delivery.Error = $"HTTP {delivery.StatusCode} (will not retry)";
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

    private const int ResponseBodyMaxBytes = 4 * 1024;

    private static async Task<string> ReadBoundedBodyAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var buf = new byte[ResponseBodyMaxBytes];
            int read = 0;
            while (read < buf.Length)
            {
                var got = await stream.ReadAsync(buf.AsMemory(read, buf.Length - read), ct);
                if (got == 0) break;
                read += got;
            }
            return Encoding.UTF8.GetString(buf, 0, read);
        }
        catch
        {
            return string.Empty;
        }
    }

    // Retry on transient failures (network, 5xx, plus 408 timeout / 429
    // rate-limit). Other 4xx responses are permanent — receiver said "no."
    private static bool IsRetryable(int statusCode)
    {
        if (statusCode >= 500) return true;
        if (statusCode == 408) return true; // request timeout
        if (statusCode == 429) return true; // too many requests
        return false;
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
