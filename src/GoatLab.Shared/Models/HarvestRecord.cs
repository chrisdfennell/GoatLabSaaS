using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GoatLab.Shared.Models;

public class HarvestRecord : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int? GoatId { get; set; }
    public Goat? Goat { get; set; }

    public DateTime HarvestDate { get; set; }

    [MaxLength(200)]
    public string? Processor { get; set; }

    /// <summary>Hanging weight in pounds</summary>
    public double? HangingWeight { get; set; }

    /// <summary>Packaged weight in pounds</summary>
    public double? PackagedWeight { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? ProcessingCost { get; set; }

    [MaxLength(200)]
    public string? LockerLocation { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
