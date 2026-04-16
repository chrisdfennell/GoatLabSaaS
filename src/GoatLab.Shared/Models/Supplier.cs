using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class Supplier : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public SupplierType SupplierType { get; set; }

    [MaxLength(200)]
    public string? ContactName { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(500)]
    public string? Website { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
