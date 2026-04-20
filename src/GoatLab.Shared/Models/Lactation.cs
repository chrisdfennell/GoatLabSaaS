using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class Lactation : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int GoatId { get; set; }
    public Goat? Goat { get; set; }

    /// <summary>Freshening date — day the doe kidded and milk recording started.</summary>
    public DateTime FreshenDate { get; set; }

    public DateTime? DryOffDate { get; set; }

    /// <summary>Optional link to the kidding that started this lactation.</summary>
    public int? KiddingRecordId { get; set; }
    public KiddingRecord? KiddingRecord { get; set; }

    /// <summary>Lactation number for this doe (1st, 2nd, etc). Auto-assigned on create.</summary>
    public int LactationNumber { get; set; } = 1;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public ICollection<MilkTestDay> TestDays { get; set; } = new List<MilkTestDay>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
