using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

// A magic-link token that lets a waitlist customer view the live status of
// their reservation without creating an account. Plaintext is shown once at
// creation, only the SHA-256 hex hash is stored. Auth is the token itself —
// there is no tenant claim, so the portal controller runs AllowAnonymous and
// resolves the tenant via the token row.
public class BuyerAccessToken : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int WaitlistEntryId { get; set; }
    public WaitlistEntry? WaitlistEntry { get; set; }

    // 64-char hex SHA-256 of the plaintext. Unique-indexed for O(1) lookup.
    [Required, MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    // First 12 chars of the plaintext, for UI display ("portal_abc12…").
    [Required, MaxLength(20)]
    public string Prefix { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
