using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class Pasture : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>GeoJSON polygon coordinates for the pasture boundary</summary>
    [MaxLength(10000)]
    public string? GeoJson { get; set; }

    /// <summary>Calculated acreage from polygon</summary>
    public double? Acreage { get; set; }

    /// <summary>Calculated perimeter in feet</summary>
    public double? PerimeterFeet { get; set; }

    public PastureCondition Condition { get; set; } = PastureCondition.Good;

    /// <summary>Max goats recommended based on acreage</summary>
    public int? StockingCapacity { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public ICollection<PastureConditionLog> ConditionLogs { get; set; } = new List<PastureConditionLog>();
    public ICollection<PastureRotation> Rotations { get; set; } = new List<PastureRotation>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
