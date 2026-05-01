using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

/// <summary>
/// A conversation thread between an anonymous buyer (visiting a public farm
/// listing) and a seller. The seller sees and replies inside GoatLab; the
/// buyer is contacted via email since they have no account.
///
/// Findable / dedup'd on (TenantId, GoatId, BuyerEmail) so a buyer asking
/// follow-up questions adds messages to the same thread instead of spawning
/// new ones. Status starts Open and can be closed by the seller when the
/// goat sells or the buyer goes cold.
/// </summary>
public class BuyerInquiry : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int GoatId { get; set; }
    public Goat? Goat { get; set; }

    [Required, MaxLength(120)]
    public string BuyerEmail { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string BuyerName { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? BuyerPhone { get; set; }

    public BuyerInquiryStatus Status { get; set; } = BuyerInquiryStatus.Open;

    /// <summary>True when the most recent message was from the buyer and the seller hasn't opened the thread yet.</summary>
    public bool UnreadForSeller { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public ICollection<BuyerInquiryMessage> Messages { get; set; } = new List<BuyerInquiryMessage>();
}

public class BuyerInquiryMessage
{
    public int Id { get; set; }

    public int InquiryId { get; set; }
    public BuyerInquiry? Inquiry { get; set; }

    /// <summary>False = from buyer (anonymous), true = from seller (authenticated).</summary>
    public bool FromSeller { get; set; }

    [Required, MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum BuyerInquiryStatus
{
    Open = 0,
    Closed = 1
}
