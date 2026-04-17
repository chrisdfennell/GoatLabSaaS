using GoatLab.Server.Data;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequiresFeature(AppFeature.SmartAlerts)]
public class AlertsController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public AlertsController(GoatLabDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<AlertDto>>> List(
        [FromQuery] bool includeDismissed = false,
        [FromQuery] int limit = 100)
    {
        if (limit < 1) limit = 1;
        if (limit > 500) limit = 500;

        var query = _db.Alerts.AsQueryable();
        if (!includeDismissed) query = query.Where(a => a.DismissedAt == null);

        var alerts = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a => new AlertDto(
                a.Id, a.Type, a.Severity, a.Title, a.Body,
                a.EntityType, a.EntityId, a.DeepLink,
                a.CreatedAt, a.ReadAt, a.DismissedAt))
            .ToListAsync();
        return alerts;
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> UnreadCount()
    {
        var count = await _db.Alerts.CountAsync(a => a.DismissedAt == null && a.ReadAt == null);
        return count;
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var alert = await _db.Alerts.FindAsync(id);
        if (alert is null) return NotFound();
        alert.ReadAt ??= DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/dismiss")]
    public async Task<IActionResult> Dismiss(int id)
    {
        var alert = await _db.Alerts.FindAsync(id);
        if (alert is null) return NotFound();
        alert.DismissedAt ??= DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("dismiss-all")]
    public async Task<IActionResult> DismissAll()
    {
        var now = DateTime.UtcNow;
        var open = await _db.Alerts.Where(a => a.DismissedAt == null).ToListAsync();
        foreach (var a in open) a.DismissedAt = now;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
