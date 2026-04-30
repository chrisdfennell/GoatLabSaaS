using System.Security.Claims;
using GoatLab.Server.Data;
using GoatLab.Server.Services;
using GoatLab.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

/// <summary>
/// Returns a checklist of onboarding steps computed from the current tenant's
/// real data. Each "done" flag is a cheap EXISTS query, scoped by the existing
/// tenant query filter. The whole checklist is dismissable per-tenant via
/// Tenant.OnboardingDismissedAt — useful for meat / brush-clearing / pet
/// operations that won't ever log a milking.
/// </summary>
[ApiController]
[Route("api/onboarding")]
public class OnboardingController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;

    public OnboardingController(GoatLabDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet("status")]
    public async Task<ActionResult<OnboardingStatus>> GetStatus()
    {
        // Per-tenant dismissal short-circuits the whole checklist. AllDone=true
        // is what the client checks to hide the card, so we just signal that.
        if (_tenantContext.TenantId is int tid)
        {
            _tenantContext.BypassFilter = true;
            var dismissed = await _db.Tenants
                .Where(t => t.Id == tid)
                .Select(t => t.OnboardingDismissedAt)
                .FirstOrDefaultAsync();
            _tenantContext.BypassFilter = false;
            if (dismissed is not null)
                return new OnboardingStatus(Array.Empty<OnboardingStep>(), 0, 0, AllDone: true);
        }

        var hasBarn = await _db.Barns.AnyAsync();
        var hasGoat = await _db.Goats.AnyAsync();
        var hasHealth = await _db.MedicalRecords.AnyAsync();
        var hasCalendar = await _db.CalendarEvents.AnyAsync();

        // Steps are deliberately operation-agnostic. Milk logging is NOT here:
        // meat goats, brush-clearers, fiber, pets, and conservation grazers
        // never milk, and pushing them through that step is friction with no
        // payoff. Health + calendar both apply universally; barn + goat are
        // table stakes. Users who want a milk-logging nudge see it in their
        // dashboard's milk widget the moment they list any does.
        var steps = new List<OnboardingStep>
        {
            new("add_barn", "Set up a barn or pen",
                "Create at least one barn so you can assign goats to pens.",
                "/map", "home_work", hasBarn),
            new("add_goat", "Add your first goat",
                "Enter a goat by hand or import your herd from CSV.",
                "/herd/add", "pets", hasGoat),
            new("log_health", "Record a health event",
                "Log a vaccination, treatment, or vet visit — set a due date and GoatLab reminds you.",
                "/health", "local_hospital", hasHealth),
            new("plan_calendar", "Add a calendar event",
                "Kiddings, breedings, hoof trims — get reminders for everything due.",
                "/calendar", "calendar_month", hasCalendar),
        };

        var done = steps.Count(s => s.Done);
        return new OnboardingStatus(steps, done, steps.Count, done == steps.Count);
    }

    [HttpPost("dismiss")]
    public async Task<IActionResult> Dismiss()
    {
        if (_tenantContext.TenantId is not int tid) return BadRequest("No active tenant.");

        _tenantContext.BypassFilter = true;
        try
        {
            await _db.Tenants
                .Where(t => t.Id == tid)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.OnboardingDismissedAt, DateTime.UtcNow));
        }
        finally { _tenantContext.BypassFilter = false; }

        return NoContent();
    }
}
