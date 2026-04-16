using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class CalendarEvent : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime Start { get; set; }
    public DateTime? End { get; set; }

    public bool AllDay { get; set; }

    [MaxLength(20)]
    public string? Color { get; set; }

    /// <summary>Optional link to a goat this event relates to</summary>
    public int? GoatId { get; set; }
    public Goat? Goat { get; set; }

    public RecurrenceInterval Recurrence { get; set; } = RecurrenceInterval.None;

    /// <summary>When this is treated as a chore, what part of the day it belongs to.</summary>
    public TaskPeriod Period { get; set; } = TaskPeriod.AnyTime;

    /// <summary>If true, the event behaves as a recurring chore (shows on /chores with completion checkboxes).</summary>
    public bool IsChore { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<EventCompletion> Completions { get; set; } = new List<EventCompletion>();
}
