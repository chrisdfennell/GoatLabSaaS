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
        var w = await _db.Webhooks.FindAsync(id);
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

        var w = await _db.Webhooks.FindAsync(id);
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
        var w = await _db.Webhooks.FindAsync(id);
        if (w is null) return NotFound();
        _db.Webhooks.Remove(w);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/test")]
    public async Task<ActionResult> Test(int id)
    {
        var w = await _db.Webhooks.FindAsync(id);
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
        if (string.IsNullOrWhiteSpace(req.Events)) return "At least one event must be subscribed.";
        var requested = req.Events.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var e in requested)
        {
            if (!WebhookEventTypes.All.Contains(e, StringComparer.OrdinalIgnoreCase))
                return $"Unknown event type: {e}";
        }
        return null;
    }

    private static string NormalizeEvents(string events)
        => string.Join(",",
            events.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.ToLowerInvariant())
                .Distinct());
}
