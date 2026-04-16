using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GoatLab.Shared.Models;

public class FeedInventory : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(200)]
    public string FeedName { get; set; } = string.Empty;

    public double QuantityOnHand { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; } // lbs, bales, bags, etc.

    public double? LowStockThreshold { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? CostPerUnit { get; set; }

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public DateTime? ExpirationDate { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
