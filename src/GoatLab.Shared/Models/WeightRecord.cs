using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class WeightRecord : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int GoatId { get; set; }
    public Goat Goat { get; set; } = null!;

    /// <summary>Weight in pounds</summary>
    public double Weight { get; set; }

    public DateTime Date { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
