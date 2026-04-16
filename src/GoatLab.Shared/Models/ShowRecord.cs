using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class ShowRecord : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int GoatId { get; set; }
    public Goat Goat { get; set; } = null!;

    public DateTime ShowDate { get; set; }

    [Required, MaxLength(200)]
    public string ShowName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Location { get; set; }

    [MaxLength(150)]
    public string? Class { get; set; }

    /// <summary>1-based placement. 1 = first place. Null for DQ / not placed.</summary>
    public int? Placing { get; set; }

    public int? ClassSize { get; set; }

    /// <summary>Chip awards like BOB, BIS, Grand Champion, Junior Champion, etc.</summary>
    [MaxLength(200)]
    public string? Awards { get; set; }

    [MaxLength(150)]
    public string? Judge { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
