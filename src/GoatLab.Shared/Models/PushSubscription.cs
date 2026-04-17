using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class PushSubscription : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    // Browser-issued endpoint URL. Unique — a user can have many devices
    // (laptop + phone) but each device ships exactly one endpoint.
    [Required, MaxLength(500)]
    public string Endpoint { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string P256dh { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Auth { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}
