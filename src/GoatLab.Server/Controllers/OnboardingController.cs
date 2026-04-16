using GoatLab.Server.Data;
using GoatLab.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

/// <summary>
/// Returns a checklist of onboarding steps computed from the current tenant's
/// real data. No new schema — each "done" flag is a cheap EXISTS query, scoped
/// by the existing tenant query filter.
/// </summary>
[ApiController]
[Route("api/onboarding")]
public class OnboardingController : ControllerBase
{
    private readonly GoatLabDbContext _db;

    public OnboardingController(GoatLabDbContext db) => _db = db;

    [HttpGet("status")]
    public async Task<ActionResult<OnboardingStatus>> GetStatus()
    {
        var hasBarn = await _db.Barns.AnyAsync();
        var hasGoat = await _db.Goats.AnyAsync();
        var hasMilk = await _db.MilkLogs.AnyAsync();
        var hasHealth = await _db.MedicalRecords.AnyAsync();
        var hasCalendar = await _db.CalendarEvents.AnyAsync();

        var steps = new List<OnboardingStep>
        {
            new("add_barn", "Set up a barn or pen",
                "Create at least one barn so you can assign goats to pens.",
                "/map", "home_work", hasBarn),
            new("add_goat", "Add your first goat",
                "Enter a goat by hand or import your herd from CSV.",
                "/herd/add", "pets", hasGoat),
            new("log_milk", "Log a milking",
                "One-tap milk logging is the fastest way to build a production history.",
                "/production", "water_drop", hasMilk),
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
}
