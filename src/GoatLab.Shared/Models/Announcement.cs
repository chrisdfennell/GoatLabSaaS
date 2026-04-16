using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public enum AnnouncementSeverity
{
    Info,
    Warning,
    Critical,
}

/// <summary>
/// Admin-authored broadcast banner shown inside the app. Scoped by time window
/// and optionally by tenant tag. Not ITenantOwned — rows are global.
/// </summary>
public class Announcement
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Body { get; set; } = string.Empty;

    public AnnouncementSeverity Severity { get; set; } = AnnouncementSeverity.Info;

    /// <summary>
    /// Only shown to tenants whose <see cref="Tenant.Tag"/> matches. Null = all tenants.
    /// </summary>
    [MaxLength(50)]
    public string? TargetTag { get; set; }

    public DateTime StartsAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndsAt { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Records that a specific user has dismissed an announcement so we don't keep
/// showing it. One row per (announcement, user).
/// </summary>
public class AnnouncementDismissal
{
    public int Id { get; set; }

    public int AnnouncementId { get; set; }
    public Announcement? Announcement { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public DateTime At { get; set; } = DateTime.UtcNow;
}
