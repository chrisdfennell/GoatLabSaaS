using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

/// <summary>
/// Marks a single occurrence of a recurring CalendarEvent as completed.
/// Combined with the parent CalendarEvent's Recurrence, the server can show
/// "today's chores" and persist which ones have been done.
/// </summary>
public class EventCompletion : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int CalendarEventId { get; set; }
    public CalendarEvent CalendarEvent { get; set; } = null!;

    /// <summary>The date of the occurrence being marked complete (date portion only).</summary>
    public DateTime OccurrenceDate { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(150)]
    public string? CompletedBy { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
