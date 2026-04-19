using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

// Seller-to-buyer handoff of a goat between two tenants on GoatLab. The seller
// initiates the transfer (creates a Pending row and gets a magic-link token);
// the buyer either accepts it (the goat row + its history move to the buyer's
// tenant) or declines/expires. Not ITenantOwned — we need to query these across
// tenants on the accept flow.
public class GoatTransfer
{
    public int Id { get; set; }

    // Source side — who is giving up the goat.
    public int FromTenantId { get; set; }
    public Tenant? FromTenant { get; set; }

    public int GoatId { get; set; }
    public Goat? Goat { get; set; }

    /// <summary>User id (string, ASP.NET Identity) of the seller who initiated.</summary>
    [Required, MaxLength(450)]
    public string InitiatedByUserId { get; set; } = string.Empty;

    // Destination side — empty until the buyer accepts.
    [Required, MaxLength(120)]
    public string BuyerEmail { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? AcceptedByUserId { get; set; }

    public int? ToTenantId { get; set; }
    public Tenant? ToTenant { get; set; }

    /// <summary>Free-form seller message shown on the buyer's accept page.</summary>
    [MaxLength(1000)]
    public string? Message { get; set; }

    public GoatTransferStatus Status { get; set; } = GoatTransferStatus.Pending;

    // Magic-link token. Plaintext is `gt_` + base64url(32) (46 chars), sent to
    // the buyer email. Only the hash is stored; the prefix (first 12 chars) is
    // kept so the UI can show the start of the link for reference.
    [Required, MaxLength(12)]
    public string TokenPrefix { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(14);
    public DateTime? AcceptedAt { get; set; }
    public DateTime? DeclinedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    [MaxLength(500)]
    public string? DeclineReason { get; set; }
}

public enum GoatTransferStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Cancelled = 3,
    Expired = 4,
}
