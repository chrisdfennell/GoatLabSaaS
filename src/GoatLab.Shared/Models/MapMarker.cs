using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class MapMarker : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public MapMarkerType MarkerType { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}
