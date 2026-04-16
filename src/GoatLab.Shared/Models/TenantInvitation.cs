using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

// A pending invite to join a tenant. The raw token is sent in the invite email
// and never stored — only its SHA256 hash lives in the DB. On accept the caller
// presents the raw token; we hash + lookup. DB leak ≠ instant invite takeover.
public class TenantInvitation : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty; // lowercased on create

    public TenantRole Role { get; set; } = TenantRole.Worker;

    [Required, MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    [Required, MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;
}
