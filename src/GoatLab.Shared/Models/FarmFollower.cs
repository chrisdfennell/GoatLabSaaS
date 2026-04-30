using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

// Anonymous email subscriber to a public farm. When the farm flips a goat to
// IsListedForSale=true, every active follower gets one notification email
// with a direct link to the listing. Single opt-in (no double-confirmation
// for MVP) because the audience here is "buyer who just landed on a farm
// page" and friction kills conversion. Every email carries an unsubscribe
// link gated on UnsubscribeToken, which honors CAN-SPAM.
//
// Cross-tenant by nature: rows belong to a tenant (the followed farm) but
// the SUBSCRIBER is anonymous — no UserId, no tenant claim.
public class FarmFollower
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(320)] // RFC 5321 max email
    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Opaque token used to authenticate the unsubscribe link without requiring
    /// a login. Hex-encoded SHA-256-able random bytes. Unique across all rows.
    /// </summary>
    [Required, MaxLength(64)]
    public string UnsubscribeToken { get; set; } = string.Empty;

    /// <summary>
    /// Soft-deactivate when the user clicks unsubscribe. We keep the row so a
    /// re-follow doesn't double-send if the email lands twice in the queue.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime? UnsubscribedAt { get; set; }

    /// <summary>Stamped when the most recent new-listing email went to this address.</summary>
    public DateTime? LastNotifiedAt { get; set; }
}
