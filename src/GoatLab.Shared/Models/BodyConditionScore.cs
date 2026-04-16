using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class BodyConditionScore : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int GoatId { get; set; }
    public Goat Goat { get; set; } = null!;

    /// <summary>BCS 1.0–5.0 (typically in 0.5 increments)</summary>
    public double Score { get; set; }

    public DateTime Date { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
