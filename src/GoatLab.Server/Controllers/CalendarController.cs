using GoatLab.Server.Data;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CalendarController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public CalendarController(GoatLabDbContext db) => _db = db;

    // --- Calendar Events ---

    [HttpGet("events")]
    public async Task<ActionResult<List<CalendarEvent>>> GetEvents([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var query = _db.CalendarEvents.Include(e => e.Goat).AsQueryable();

        if (from.HasValue) query = query.Where(e => e.Start >= from.Value || (e.End != null && e.End >= from.Value));
        if (to.HasValue) query = query.Where(e => e.Start <= to.Value);

        return await query.OrderBy(e => e.Start).ToListAsync();
    }

    [HttpGet("events/{id}")]
    public async Task<ActionResult<CalendarEvent>> GetEvent(int id)
    {
        var ev = await _db.CalendarEvents.Include(e => e.Goat).FirstOrDefaultAsync(e => e.Id == id);
        return ev is null ? NotFound() : ev;
    }

    [HttpPost("events")]
    public async Task<ActionResult<CalendarEvent>> CreateEvent(CalendarEvent ev)
    {
        ev.CreatedAt = DateTime.UtcNow;
        _db.CalendarEvents.Add(ev);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetEvent), new { id = ev.Id }, ev);
    }

    [HttpPut("events/{id}")]
    public async Task<IActionResult> UpdateEvent(int id, CalendarEvent ev)
    {
        if (id != ev.Id) return BadRequest();
        var existing = await _db.CalendarEvents.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Title = ev.Title;
        existing.Description = ev.Description;
        existing.Start = ev.Start;
        existing.End = ev.End;
        existing.AllDay = ev.AllDay;
        existing.Color = ev.Color;
        existing.GoatId = ev.GoatId;
        existing.Recurrence = ev.Recurrence;
        existing.Period = ev.Period;
        existing.IsChore = ev.IsChore;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("events/{id}")]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _db.CalendarEvents.FindAsync(id);
        if (ev is null) return NotFound();
        _db.CalendarEvents.Remove(ev);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Checklists ---

    [HttpGet("checklists")]
    public async Task<ActionResult<List<Checklist>>> GetChecklists()
    {
        return await _db.Checklists
            .Include(c => c.Items.OrderBy(i => i.SortOrder))
            .OrderBy(c => c.Period)
            .ToListAsync();
    }

    [HttpPost("checklists")]
    public async Task<ActionResult<Checklist>> CreateChecklist(Checklist checklist)
    {
        _db.Checklists.Add(checklist);
        await _db.SaveChangesAsync();
        return Ok(checklist);
    }

    [HttpPut("checklists/{id}")]
    public async Task<IActionResult> UpdateChecklist(int id, Checklist checklist)
    {
        if (id != checklist.Id) return BadRequest();
        var existing = await _db.Checklists.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id);
        if (existing is null) return NotFound();

        existing.Title = checklist.Title;
        existing.Period = checklist.Period;

        // Replace items
        _db.ChecklistItems.RemoveRange(existing.Items);
        foreach (var item in checklist.Items)
        {
            item.ChecklistId = id;
            _db.ChecklistItems.Add(item);
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("checklists/{id}")]
    public async Task<IActionResult> DeleteChecklist(int id)
    {
        var checklist = await _db.Checklists.FindAsync(id);
        if (checklist is null) return NotFound();
        _db.Checklists.Remove(checklist);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Daily Completions ---

    [HttpGet("completions")]
    public async Task<ActionResult<List<ChecklistCompletion>>> GetCompletions([FromQuery] DateTime date)
    {
        return await _db.ChecklistCompletions
            .Include(c => c.ChecklistItem)
            .Where(c => c.Date.Date == date.Date)
            .ToListAsync();
    }

    [HttpPost("completions/toggle")]
    public async Task<ActionResult<ChecklistCompletion>> ToggleCompletion([FromQuery] int checklistItemId, [FromQuery] DateTime date)
    {
        var existing = await _db.ChecklistCompletions
            .FirstOrDefaultAsync(c => c.ChecklistItemId == checklistItemId && c.Date.Date == date.Date);

        if (existing != null)
        {
            existing.IsCompleted = !existing.IsCompleted;
            existing.CompletedAt = existing.IsCompleted ? DateTime.UtcNow : null;
        }
        else
        {
            existing = new ChecklistCompletion
            {
                ChecklistItemId = checklistItemId,
                Date = date.Date,
                IsCompleted = true,
                CompletedAt = DateTime.UtcNow
            };
            _db.ChecklistCompletions.Add(existing);
        }

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    // --- Recurring event expansion + completion ---

    /// <summary>
    /// Materializes virtual occurrences of recurring CalendarEvents within a date range.
    /// Single (Recurrence=None) events are returned as-is. Recurring ones spawn one
    /// occurrence per interval that falls inside the window.
    /// </summary>
    [HttpGet("expanded")]
    public async Task<ActionResult<List<object>>> GetExpanded(
        [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] bool? choresOnly)
    {
        var query = _db.CalendarEvents.Include(e => e.Goat).AsQueryable();
        if (choresOnly == true) query = query.Where(e => e.IsChore);
        var events = await query.ToListAsync();

        var fromDate = from.Date;
        var toDate = to.Date;

        var completions = await _db.EventCompletions
            .Where(c => c.OccurrenceDate >= fromDate && c.OccurrenceDate <= toDate)
            .ToListAsync();

        var occurrences = new List<object>();
        foreach (var ev in events)
        {
            foreach (var occ in EnumerateOccurrences(ev, fromDate, toDate))
            {
                var completion = completions.FirstOrDefault(c =>
                    c.CalendarEventId == ev.Id && c.OccurrenceDate.Date == occ.Date);

                occurrences.Add(new
                {
                    eventId = ev.Id,
                    title = ev.Title,
                    description = ev.Description,
                    occurrenceDate = occ,
                    period = ev.Period.ToString(),
                    isChore = ev.IsChore,
                    color = ev.Color,
                    goatId = ev.GoatId,
                    goatName = ev.Goat?.Name,
                    recurrence = ev.Recurrence.ToString(),
                    completed = completion != null,
                    completionId = completion?.Id
                });
            }
        }
        return Ok(occurrences.OrderBy(o => ((dynamic)o).occurrenceDate));
    }

    private static IEnumerable<DateTime> EnumerateOccurrences(CalendarEvent ev, DateTime fromDate, DateTime toDate)
    {
        var first = ev.Start.Date;
        if (ev.Recurrence == RecurrenceInterval.None)
        {
            if (first >= fromDate && first <= toDate) yield return first;
            yield break;
        }

        var cursor = first;
        while (cursor < fromDate)
        {
            var next = Advance(cursor, ev.Recurrence);
            if (next == cursor) yield break; // safety
            cursor = next;
            if (cursor > toDate) yield break;
        }

        while (cursor <= toDate)
        {
            yield return cursor;
            cursor = Advance(cursor, ev.Recurrence);
        }
    }

    private static DateTime Advance(DateTime from, RecurrenceInterval r) => r switch
    {
        RecurrenceInterval.Weekly      => from.AddDays(7),
        RecurrenceInterval.BiWeekly    => from.AddDays(14),
        RecurrenceInterval.Monthly     => from.AddMonths(1),
        RecurrenceInterval.Quarterly   => from.AddMonths(3),
        RecurrenceInterval.BiAnnually  => from.AddMonths(6),
        RecurrenceInterval.Annually    => from.AddYears(1),
        _                              => from.AddYears(100)
    };

    [HttpPost("events/{id}/complete")]
    public async Task<ActionResult<EventCompletion>> CompleteOccurrence(int id, [FromBody] CompleteOccurrenceRequest req)
    {
        var ev = await _db.CalendarEvents.FindAsync(id);
        if (ev is null) return NotFound();

        var existing = await _db.EventCompletions
            .FirstOrDefaultAsync(c => c.CalendarEventId == id && c.OccurrenceDate == req.OccurrenceDate.Date);
        if (existing != null) return Ok(existing);

        var completion = new EventCompletion
        {
            CalendarEventId = id,
            OccurrenceDate = req.OccurrenceDate.Date,
            CompletedAt = DateTime.UtcNow,
            CompletedBy = req.CompletedBy,
            Notes = req.Notes
        };
        _db.EventCompletions.Add(completion);
        await _db.SaveChangesAsync();
        return Ok(completion);
    }

    [HttpDelete("events/{id}/complete")]
    public async Task<IActionResult> UncompleteOccurrence(int id, [FromQuery] DateTime occurrenceDate)
    {
        var existing = await _db.EventCompletions
            .FirstOrDefaultAsync(c => c.CalendarEventId == id && c.OccurrenceDate == occurrenceDate.Date);
        if (existing is null) return NoContent();
        _db.EventCompletions.Remove(existing);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class CompleteOccurrenceRequest
{
    public DateTime OccurrenceDate { get; set; }
    public string? CompletedBy { get; set; }
    public string? Notes { get; set; }
}
