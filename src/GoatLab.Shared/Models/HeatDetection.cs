using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class HeatDetection : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int GoatId { get; set; }
    public Goat? Goat { get; set; }

    public DateTime DetectedDate { get; set; }

    /// <summary>Predicted next heat (~21 day cycle)</summary>
    public DateTime? PredictedNextHeat { get; set; }

    [MaxLength(500)]
    public string? Signs { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
