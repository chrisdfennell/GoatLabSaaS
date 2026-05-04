using System.Security.Cryptography;
using GoatLab.Server.Data;
using GoatLab.Server.Services.Plans;
using GoatLab.Server.Services.Webhooks;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequiresFeature(AppFeature.WebhooksAndApi)]
public class WebhooksController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly WebhookDispatcher _dispatcher;
    public WebhooksController(GoatLabDbContext db, WebhookDispatcher dispatcher)
    {
        _db = db;
        _dispatcher = dispatcher;
    }

    // Shape returned on list/read — Secret is stripped except on the row just created.
    public record WebhookDto(
        int Id, string Name, string Url, string Events, bool IsActive,
        DateTime CreatedAt, DateTime UpdatedAt,
        DateTime? LastDeliveredAt, int? LastStatusCode, string? LastError);

    public record CreatedWebhookDto(
        int Id, string Name, string Url, string Events, bool IsActive,
        DateTime CreatedAt,
        string Secret); // shown once

    public record CreateOrUpdateRequest(string Name, string Url, string Events, bool IsActive);

    [HttpGet]
    public async Task<ActionResult<List<WebhookDto>>> List()
    {
        return await _db.Webhooks
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WebhookDto(w.Id, w.Name, w.Url, w.Events, w.IsActive,
                w.CreatedAt, w.UpdatedAt, w.LastDeliveredAt, w.LastStatusCode, w.LastError))
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WebhookDto>> Get(int id)
    {
        var w = await _db.Webhooks.FirstOrDefaultAsync(w => w.Id == id);
        if (w is null) return NotFound();
        return new WebhookDto(w.Id, w.Name, w.Url, w.Events, w.IsActive,
            w.CreatedAt, w.UpdatedAt, w.LastDeliveredAt, w.LastStatusCode, w.LastError);
    }

    [HttpPost]
    public async Task<ActionResult<CreatedWebhookDto>> Create([FromBody] CreateOrUpdateRequest req)
    {
        var error = ValidateRequest(req);
        if (error != null) return BadRequest(new { error });

        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var webhook = new Webhook
        {
            Name = req.Name.Trim(),
            Url = req.Url.Trim(),
            Secret = secret,
            Events = NormalizeEvents(req.Events),
            IsActive = req.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Webhooks.Add(webhook);
        await _db.SaveChangesAsync();

        return new CreatedWebhookDto(webhook.Id, webhook.Name, webhook.Url, webhook.Events,
            webhook.IsActive, webhook.CreatedAt, secret);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateOrUpdateRequest req)
    {
        var error = ValidateRequest(req);
        if (error != null) return BadRequest(new { error });

        var w = await _db.Webhooks.FirstOrDefaultAsync(w => w.Id == id);
        if (w is null) return NotFound();

        w.Name = req.Name.Trim();
        w.Url = req.Url.Trim();
        w.Events = NormalizeEvents(req.Events);
        w.IsActive = req.IsActive;
        w.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var w = await _db.Webhooks.FirstOrDefaultAsync(w => w.Id == id);
        if (w is null) return NotFound();
        _db.Webhooks.Remove(w);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/test")]
    public async Task<ActionResult> Test(int id)
    {
        var w = await _db.Webhooks.FirstOrDefaultAsync(w => w.Id == id);
        if (w is null) return NotFound();

        // Temporarily force subscription to "ping" so the dispatcher fans out
        // even if the user hasn't opted into any events yet.
        var originalEvents = w.Events;
        w.Events = string.IsNullOrWhiteSpace(originalEvents)
            ? WebhookEventTypes.Ping
            : originalEvents + "," + WebhookEventTypes.Ping;
        try
        {
            await _dispatcher.DispatchAsync(WebhookEventTypes.Ping, new { message = "GoatLab test ping" });
        }
        finally
        {
            w.Events = originalEvents;
            await _db.SaveChangesAsync();
        }
        return Ok(new { sent = true });
    }

    public record DeliveryDto(
        int Id, string EventType, string DeliveryId, int AttemptCount,
        int? StatusCode, string? Error, DateTime CreatedAt, DateTime? DeliveredAt, DateTime? NextRetryAt);

    [HttpGet("{id}/deliveries")]
    public async Task<ActionResult<List<DeliveryDto>>> Deliveries(int id)
    {
        return await _db.WebhookDeliveries
            .Where(d => d.WebhookId == id)
            .OrderByDescending(d => d.CreatedAt)
            .Take(50)
            .Select(d => new DeliveryDto(d.Id, d.EventType, d.DeliveryId, d.AttemptCount,
                d.StatusCode, d.Error, d.CreatedAt, d.DeliveredAt, d.NextRetryAt))
            .ToListAsync();
    }

    private static string? ValidateRequest(CreateOrUpdateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return "Name is required.";
        if (string.IsNullOrWhiteSpace(req.Url)) return "Url is required.";
        if (!Uri.TryCreate(req.Url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return "Url must be a full http:// or https:// URL.";
        if (IsInternalHost(uri.Host))
            return "Url must point to a public host. Localhost and private network addresses are not allowed.";
        if (string.IsNullOrWhiteSpace(req.Events)) return "At least one event must be subscribed.";
        var requested = req.Events.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var e in requested)
        {
            if (!WebhookEventTypes.All.Contains(e, StringComparer.OrdinalIgnoreCase))
                return $"Unknown event type: {e}";
        }
        return null;
    }

    // Block obvious SSRF targets at registration time. We can't catch every
    // case (DNS rebinding, internal hostnames the user knows about) without a
    // runtime check in the dispatcher, but rejecting localhost / RFC1918 /
    // link-local literals stops the easy mistakes.
    private static bool IsInternalHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return true;
        var lower = host.ToLowerInvariant();
        if (lower is "localhost" or "ip6-localhost" or "ip6-loopback") return true;
        if (lower.EndsWith(".localhost") || lower.EndsWith(".local") || lower.EndsWith(".internal"))
            return true;
        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            if (System.Net.IPAddress.IsLoopback(ip)) return true;
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                // 10.0.0.0/8
                if (b[0] == 10) return true;
                // 172.16.0.0/12
                if (b[0] == 172 && (b[1] & 0xF0) == 16) return true;
                // 192.168.0.0/16
                if (b[0] == 192 && b[1] == 168) return true;
                // 169.254.0.0/16 link-local (also catches AWS/Azure metadata 169.254.169.254)
                if (b[0] == 169 && b[1] == 254) return true;
                // 0.0.0.0/8 "this network"
                if (b[0] == 0) return true;
            }
            else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                // Treat any non-global v6 address as internal: link-local, ULA, loopback, unspecified.
                if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal) return true;
                if (ip.Equals(System.Net.IPAddress.IPv6Any)) return true;
            }
        }
        return false;
    }

    private static string NormalizeEvents(string events)
        => string.Join(",",
            events.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.ToLowerInvariant())
                .Distinct());
}
