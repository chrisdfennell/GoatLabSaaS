using System.Security.Claims;
using GoatLab.Server.Data;
using GoatLab.Server.Services;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

/// <summary>
/// Tenant-facing announcements: list active ones for the current user, dismiss.
/// Announcements are authored in /admin/announcements. Visibility filter:
/// active + within time window + either no target tag or matches current tenant tag
/// + not already dismissed by this user.
/// </summary>
[ApiController]
[Route("api/announcements")]
public class AnnouncementsController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AnnouncementsController(GoatLabDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet("active")]
    public async Task<ActionResult<List<ActiveAnnouncement>>> GetActive()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return new List<ActiveAnnouncement>();

        // Resolve the current tenant's tag (if any) so we can match TargetTag.
        string? tenantTag = null;
        if (_tenantContext.TenantId is int tid)
        {
            _tenantContext.BypassFilter = true;
            tenantTag = await _db.Tenants.Where(t => t.Id == tid)
                .Select(t => t.Tag).FirstOrDefaultAsync();
        }

        var now = DateTime.UtcNow;

        _tenantContext.BypassFilter = true;
        var dismissedIds = await _db.AnnouncementDismissals
            .Where(d => d.UserId == userId)
            .Select(d => d.AnnouncementId)
            .ToListAsync();

        var items = await _db.Announcements
            .Where(a => a.IsActive && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt >= now))
            .Where(a => a.TargetTag == null || a.TargetTag == tenantTag)
            .Where(a => !dismissedIds.Contains(a.Id))
            .OrderByDescending(a => a.Severity).ThenByDescending(a => a.CreatedAt)
            .Select(a => new ActiveAnnouncement(a.Id, a.Title, a.Body, a.Severity.ToString()))
            .ToListAsync();

        return items;
    }

    [HttpPost("{id}/dismiss")]
    public async Task<IActionResult> Dismiss(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        _tenantContext.BypassFilter = true;
        var exists = await _db.AnnouncementDismissals
            .AnyAsync(d => d.AnnouncementId == id && d.UserId == userId);
        if (exists) return NoContent();

        _db.AnnouncementDismissals.Add(new AnnouncementDismissal
        {
            AnnouncementId = id,
            UserId = userId,
        });
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { /* unique constraint race — another click won. */ }
        return NoContent();
    }
}
