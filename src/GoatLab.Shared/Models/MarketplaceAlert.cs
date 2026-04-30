using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

// Demand-side analog to FarmFollower: an anonymous buyer registers a SEARCH
// (breed × sex × price range × state) and gets one email per matching new
// listing. Fired from the same IsListedForSale=true hook that emails farm
// followers, so a single goat-update event can touch both audiences.
//
// Cross-tenant by nature — the email belongs to a buyer, not a tenant. Not
// ITenantOwned. All filter fields are optional; null means "any".
public class MarketplaceAlert
{
    public int Id { get; set; }

    [Required, MaxLength(320)] // RFC 5321 max email
    public string Email { get; set; } = string.Empty;

    /// <summary>Normalized via PublicController.BreedSlug at insert time. Null = any breed.</summary>
    [MaxLength(80)]
    public string? BreedSlug { get; set; }

    /// <summary>Stored as the Gender enum's string name (Male/Female/Wether). Null = any.</summary>
    [MaxLength(16)]
    public string? Sex { get; set; }

    public int? MinPriceCents { get; set; }
    public int? MaxPriceCents { get; set; }

    /// <summary>
    /// Substring match against Tenant.Location (case-insensitive). Null = any
    /// state. Stored as the buyer typed it; comparison is plain Contains so
    /// "Texas" / "TX" / "Austin, TX" all work depending on what the seller
    /// has on their farm settings.
    /// </summary>
    [MaxLength(80)]
    public string? StateMatch { get; set; }

    /// <summary>Hex64 random token; unique. Drives /alerts/unsubscribe?token=...</summary>
    [Required, MaxLength(64)]
    public string UnsubscribeToken { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UnsubscribedAt { get; set; }

    /// <summary>Stamped after each fan-out email so we have a sense of activity per alert.</summary>
    public DateTime? LastNotifiedAt { get; set; }
}
