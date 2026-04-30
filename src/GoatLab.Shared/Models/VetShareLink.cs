using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

// Time-bounded URL the owner generates and hands to their vet so the vet can
// see one goat's medical history without signing up. Vet can also leave a
// note that lands back in the goat's MedicalRecord history.
//
// Cross-tenant lookup by token (no tenant context on anon requests), so this
// entity intentionally is NOT ITenantOwned — the controller enforces scope by
// matching the request's hashed token against the stored hash.
public class VetShareLink
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int GoatId { get; set; }
    public Goat? Goat { get; set; }

    /// <summary>User who minted the link. Used for the audit trail when the vet leaves a note.</summary>
    [Required, MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>First 12 chars of the plaintext token. Lets the seller see/identify a link without revealing the secret.</summary>
    [Required, MaxLength(12)]
    public string TokenPrefix { get; set; } = string.Empty;

    /// <summary>SHA-256 hex of the plaintext token. Unique index — collision would be a generator bug.</summary>
    [Required, MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastViewedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    /// <summary>Optional pre-fill: the vet's name. Used to greet them on the page and to author notes when the vet doesn't type their name.</summary>
    [MaxLength(120)]
    public string? VetName { get; set; }

    /// <summary>Optional: emailing the link directly to the vet.</summary>
    [MaxLength(320)]
    public string? VetEmail { get; set; }
}
