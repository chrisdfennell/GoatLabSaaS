using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class Barn : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Optional map position — null if not placed yet.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public ICollection<Pen> Pens { get; set; } = new List<Pen>();
}
