using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

/// <summary>
/// Lightweight account for marketplace buyers — no tenant, no password, no
/// Identity stack. Created (or matched) when a buyer clicks a magic-link
/// signin from email. Lets them save listings, see their inquiry history,
/// and follow farms across devices.
///
/// Deliberately separate from ApplicationUser so a buyer doesn't accidentally
/// land on the seller dashboard, and so we can reuse the same email for a
/// future seller registration without surgery.
/// </summary>
public class BuyerAccount
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? DisplayName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public ICollection<BuyerSavedListing> Saved { get; set; } = new List<BuyerSavedListing>();
}

public class BuyerSavedListing
{
    public int Id { get; set; }

    public int BuyerAccountId { get; set; }
    public BuyerAccount? Account { get; set; }

    public int GoatId { get; set; }
    public Goat? Goat { get; set; }

    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// One-time magic-link token. Hashed at rest (the plaintext only ever leaves
/// in the email body) so a DB leak doesn't enable replay. Expires after a
/// short window; UsedAt prevents replay even before expiry.
/// </summary>
public class BuyerSignInToken
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Email { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
