using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class PastureConditionLog : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int PastureId { get; set; }
    public Pasture Pasture { get; set; } = null!;

    public PastureCondition Condition { get; set; }

    public DateTime Date { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
