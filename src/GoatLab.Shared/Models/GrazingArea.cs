using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class GrazingArea : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>GeoJSON polygon for KML export and display</summary>
    [MaxLength(10000)]
    public string? GeoJson { get; set; }

    public double? Acreage { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
