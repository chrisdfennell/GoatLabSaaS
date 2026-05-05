using System.Text;
using GoatLab.Server.Data;
using GoatLab.Server.Services;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Public iCal subscription endpoint — anyone with the URL can read the
// tenant's calendar. The token in the URL is the shared secret; rotating it
// from /calendar invalidates all existing subscriptions. Bypasses tenant
// filters because the caller has no auth cookie.
[AllowAnonymous]
public class CalendarFeedController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CalendarFeedController(GoatLabDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet("/calendar/{token}.ics")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Feed(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length is < 8 or > 64)
            return NotFound();

        _tenantContext.BypassFilter = true;

        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.CalendarFeedToken == token && t.DeletedAt == null && t.SuspendedAt == null)
            .Select(t => new { t.Id, t.Name })
            .FirstOrDefaultAsync(ct);
        if (tenant is null) return NotFound();

        var events = await _db.CalendarEvents.IgnoreQueryFilters()
            .Where(e => e.TenantId == tenant.Id)
            .Include(e => e.Goat)
            .ToListAsync(ct);

        var ics = BuildIcs(tenant.Name, events);
        return File(Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", $"goatlab-{tenant.Id}.ics");
    }

    internal static string BuildIcs(string tenantName, IReadOnlyList<CalendarEvent> events)
    {
        var sb = new StringBuilder();
        sb.Append("BEGIN:VCALENDAR\r\n");
        sb.Append("VERSION:2.0\r\n");
        sb.Append("PRODID:-//GoatLab//Calendar Feed//EN\r\n");
        sb.Append("CALSCALE:GREGORIAN\r\n");
        sb.Append("METHOD:PUBLISH\r\n");
        sb.Append($"X-WR-CALNAME:{Escape($"{tenantName} — GoatLab")}\r\n");
        sb.Append("X-WR-TIMEZONE:UTC\r\n");

        var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");

        foreach (var ev in events)
        {
            sb.Append("BEGIN:VEVENT\r\n");
            sb.Append($"UID:goatlab-event-{ev.Id}@goatlab.app\r\n");
            sb.Append($"DTSTAMP:{stamp}\r\n");

            if (ev.AllDay)
            {
                // All-day events are date-only in iCal — the time portion has
                // no meaning. ToUniversalTime() on a Kind=Unspecified value
                // (which is what EF Core hands back from SQL Server unless we
                // tell it otherwise) treats it as local-time and shifts the
                // date by the server's TZ offset, which can flip the calendar
                // entry by a day. Format the calendar date directly instead.
                sb.Append($"DTSTART;VALUE=DATE:{ev.Start:yyyyMMdd}\r\n");
                var endDate = (ev.End ?? ev.Start).AddDays(1);
                sb.Append($"DTEND;VALUE=DATE:{endDate:yyyyMMdd}\r\n");
            }
            else
            {
                sb.Append($"DTSTART:{ToUtc(ev.Start)}\r\n");
                sb.Append($"DTEND:{ToUtc(ev.End ?? ev.Start.AddHours(1))}\r\n");
            }

            var title = ev.Title;
            if (ev.Goat is not null) title = $"{ev.Goat.Name} — {title}";
            sb.Append($"SUMMARY:{Escape(title)}\r\n");
            if (!string.IsNullOrWhiteSpace(ev.Description))
                sb.Append($"DESCRIPTION:{Escape(ev.Description)}\r\n");

            var rrule = MapRrule(ev.Recurrence);
            if (rrule is not null) sb.Append($"RRULE:{rrule}\r\n");

            sb.Append("END:VEVENT\r\n");
        }

        sb.Append("END:VCALENDAR\r\n");
        return sb.ToString();
    }

    private static string ToUtc(DateTime dt)
    {
        var utc = dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return utc.ToString("yyyyMMddTHHmmssZ");
    }

    private static string? MapRrule(RecurrenceInterval r) => r switch
    {
        RecurrenceInterval.Weekly => "FREQ=WEEKLY",
        RecurrenceInterval.BiWeekly => "FREQ=WEEKLY;INTERVAL=2",
        RecurrenceInterval.Monthly => "FREQ=MONTHLY",
        RecurrenceInterval.Quarterly => "FREQ=MONTHLY;INTERVAL=3",
        RecurrenceInterval.BiAnnually => "FREQ=MONTHLY;INTERVAL=6",
        RecurrenceInterval.Annually => "FREQ=YEARLY",
        _ => null
    };

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\")
         .Replace(";", "\\;")
         .Replace(",", "\\,")
         .Replace("\r\n", "\\n")
         .Replace("\n", "\\n");
}
