using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class FamachaScore : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int GoatId { get; set; }
    public Goat Goat { get; set; } = null!;

    /// <summary>FAMACHA score 1–5 (1=red/healthy, 5=white/severe anemia)</summary>
    public int Score { get; set; }

    public DateTime Date { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
