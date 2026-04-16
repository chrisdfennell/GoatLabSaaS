using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class Kid : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int KiddingRecordId { get; set; }
    public KiddingRecord KiddingRecord { get; set; } = null!;

    [MaxLength(100)]
    public string? Name { get; set; }

    public Gender Gender { get; set; }

    public double? BirthWeightLbs { get; set; }

    public KidPresentation Presentation { get; set; } = KidPresentation.Normal;
    public KidVigor Vigor { get; set; } = KidVigor.Strong;

    /// <summary>If this kid was kept in the herd, points to the Goat record created from it.</summary>
    public int? LinkedGoatId { get; set; }
    public Goat? LinkedGoat { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
