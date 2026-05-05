using GoatLab.Server.Controllers;
using GoatLab.Shared.Models;

namespace GoatLab.Tests;

// Pinning the iCal output for all-day events so a future refactor doesn't
// reintroduce the ToUniversalTime() shift on Kind=Unspecified DateTimes
// that flipped events by ±1 day in subscribers' calendar apps.
public class CalendarFeedIcsTests
{
    private static CalendarEvent NewEvent(DateTime start, bool allDay, DateTime? end = null) => new()
    {
        Id = 1,
        TenantId = 1,
        Title = "Hoof trim day",
        Start = start,
        End = end,
        AllDay = allDay,
        Recurrence = RecurrenceInterval.None,
    };

    [Fact]
    public void All_day_event_emits_DTSTART_as_the_stored_calendar_date()
    {
        // Stored as midnight UTC for May 15. The previous bug treated this as
        // local time on a non-UTC server, which could shift the date to May 14
        // or May 16 in the iCal feed.
        var start = new DateTime(2024, 5, 15, 0, 0, 0, DateTimeKind.Utc);
        var ics = CalendarFeedController.BuildIcs("Acme", new[] { NewEvent(start, allDay: true) });

        Assert.Contains("DTSTART;VALUE=DATE:20240515", ics);
        // End-exclusive per iCal spec — single-day event ends on May 16.
        Assert.Contains("DTEND;VALUE=DATE:20240516", ics);
    }

    [Fact]
    public void All_day_event_with_unspecified_kind_is_not_shifted()
    {
        // EF Core's default mapping returns Kind=Unspecified for DateTime
        // columns. The fix formats the calendar date directly instead of
        // ToUniversalTime()-converting (which would treat Unspecified as
        // local and shift by the server's TZ offset).
        var start = new DateTime(2024, 5, 15, 0, 0, 0, DateTimeKind.Unspecified);
        var ics = CalendarFeedController.BuildIcs("Acme", new[] { NewEvent(start, allDay: true) });

        Assert.Contains("DTSTART;VALUE=DATE:20240515", ics);
    }

    [Fact]
    public void Multi_day_all_day_event_uses_end_plus_one()
    {
        var start = new DateTime(2024, 5, 15, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 5, 17, 0, 0, 0, DateTimeKind.Utc);
        var ics = CalendarFeedController.BuildIcs("Acme", new[] { NewEvent(start, allDay: true, end) });

        Assert.Contains("DTSTART;VALUE=DATE:20240515", ics);
        // iCal end is exclusive, so May 17 stored end → DTEND May 18.
        Assert.Contains("DTEND;VALUE=DATE:20240518", ics);
    }

    [Fact]
    public void Timed_event_still_emits_zulu_timestamps()
    {
        var start = new DateTime(2024, 5, 15, 14, 30, 0, DateTimeKind.Utc);
        var ics = CalendarFeedController.BuildIcs("Acme", new[] { NewEvent(start, allDay: false) });

        Assert.Contains("DTSTART:20240515T143000Z", ics);
        // No end provided — controller defaults to start + 1 hour.
        Assert.Contains("DTEND:20240515T153000Z", ics);
    }
}
