using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class PastureConditionLog : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int PastureId { get; set; }
    // Nullable so POST bodies that only carry PastureId don't trip
    // ASP.NET Core's non-nullable-reference model validator.
    public Pasture? Pasture { get; set; }

    public PastureCondition Condition { get; set; }

    public DateTime Date { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
