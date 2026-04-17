using System.Security.Claims;
using GoatLab.Server.Data;
using GoatLab.Server.Services.Plans;
using GoatLab.Server.Services.Push;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequiresFeature(AppFeature.PushNotifications)]
public class PushController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly PushService _push;

    public PushController(GoatLabDbContext db, PushService push)
    {
        _db = db;
        _push = push;
    }

    // Used by the client to bootstrap subscribe(): browsers need the VAPID
    // public key (URL-safe base64) before they'll create a subscription.
    [HttpGet("vapid-public-key")]
    public ActionResult<VapidPublicKeyResponse> GetPublicKey()
    {
        if (!_push.IsConfigured) return NotFound(new { error = "Push is not configured on this server." });
        return new VapidPublicKeyResponse(_push.PublicKey!);
    }

    [HttpGet("subscriptions")]
    public async Task<ActionResult<List<PushSubscriptionDto>>> List()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var subs = await _db.PushSubscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new PushSubscriptionDto(s.Id, s.Endpoint, s.UserAgent, s.CreatedAt, s.LastUsedAt))
            .ToListAsync();
        return subs;
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe(PushSubscribeRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Endpoint) ||
            string.IsNullOrWhiteSpace(req.P256dh) ||
            string.IsNullOrWhiteSpace(req.Auth))
        {
            return BadRequest(new { error = "Endpoint, p256dh, and auth are all required." });
        }

        // Same browser/device re-subscribing → update keys + bump LastUsedAt
        // instead of creating duplicates.
        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == req.Endpoint);
        if (existing is not null)
        {
            existing.UserId = userId;
            existing.P256dh = req.P256dh;
            existing.Auth = req.Auth;
            existing.UserAgent = req.UserAgent;
            existing.LastUsedAt = DateTime.UtcNow;
        }
        else
        {
            _db.PushSubscriptions.Add(new PushSubscription
            {
                UserId = userId,
                Endpoint = req.Endpoint,
                P256dh = req.P256dh,
                Auth = req.Auth,
                UserAgent = req.UserAgent,
            });
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var sub = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == req.Endpoint && s.UserId == userId);
        if (sub is null) return NoContent();

        _db.PushSubscriptions.Remove(sub);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("subscriptions/{id}")]
    public async Task<IActionResult> Remove(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var sub = await _db.PushSubscriptions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (sub is null) return NotFound();

        _db.PushSubscriptions.Remove(sub);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("test")]
    public async Task<IActionResult> SendTest()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await _push.SendToUserAsync(userId, new PushPayload(
            "GoatLab test notification",
            "If you can see this, push is working.",
            "/alerts"));
        return NoContent();
    }

    public record UnsubscribeRequest(string Endpoint);
}
