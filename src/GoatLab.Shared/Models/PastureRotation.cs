using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class PastureRotation : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int PastureId { get; set; }
    public Pasture Pasture { get; set; } = null!;

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>Number of goats in this rotation</summary>
    public int GoatCount { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
