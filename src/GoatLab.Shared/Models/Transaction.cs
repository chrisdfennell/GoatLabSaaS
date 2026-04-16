using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GoatLab.Shared.Models;

public class Transaction : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public TransactionType Type { get; set; }

    public DateTime Date { get; set; }

    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    /// <summary>Category — use ExpenseCategory or IncomeCategory enum name as string</summary>
    [MaxLength(100)]
    public string? Category { get; set; }

    /// <summary>Optional link to a specific goat for cost-per-goat analysis</summary>
    public int? GoatId { get; set; }
    public Goat? Goat { get; set; }

    /// <summary>Auto-created from a Sale. Mirrored payments live here so finance totals reflect sales.</summary>
    public int? SaleId { get; set; }

    /// <summary>Auto-created from a Purchase (mirror of SaleId for the expense side).</summary>
    public int? PurchaseId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
