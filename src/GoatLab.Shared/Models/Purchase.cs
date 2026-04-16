using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GoatLab.Shared.Models;

public class Purchase : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public DateTime PurchaseDate { get; set; }

    [MaxLength(200)]
    public string? SellerName { get; set; }

    /// <summary>Optional supplier link if you've already saved this seller.</summary>
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>If this purchase brought a goat into the herd, point to it.</summary>
    public int? GoatId { get; set; }
    public Goat? Goat { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
