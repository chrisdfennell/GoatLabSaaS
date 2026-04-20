using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class MilkLog : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int GoatId { get; set; }
    public Goat? Goat { get; set; }

    public DateTime Date { get; set; }

    /// <summary>Amount in pounds</summary>
    public double Amount { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
