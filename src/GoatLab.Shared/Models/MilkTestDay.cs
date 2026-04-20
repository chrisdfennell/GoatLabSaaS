using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GoatLab.Shared.Models;

/// <summary>
/// DHIR-style test day. Components (butterfat, protein, SCC) are optional for
/// operators who only want volume tracking.
/// </summary>
public class MilkTestDay : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int LactationId { get; set; }
    public Lactation? Lactation { get; set; }

    /// <summary>Denormalized for easy per-goat queries.</summary>
    public int GoatId { get; set; }

    public DateTime TestDate { get; set; }

    public double? AmLbs { get; set; }
    public double? PmLbs { get; set; }

    /// <summary>Total for the day. If not supplied, server sums AM + PM.</summary>
    public double TotalLbs { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? ButterfatPercent { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? ProteinPercent { get; set; }

    /// <summary>Somatic Cell Count in thousands (e.g. 250 = 250,000/mL).</summary>
    public int? SomaticCellCount { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
