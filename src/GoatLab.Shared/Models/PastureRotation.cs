using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class PastureRotation : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int PastureId { get; set; }
    // Nullable so POST bodies that only carry PastureId don't trip
    // ASP.NET Core's non-nullable-reference model validator.
    public Pasture? Pasture { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>Number of goats in this rotation</summary>
    public int GoatCount { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
