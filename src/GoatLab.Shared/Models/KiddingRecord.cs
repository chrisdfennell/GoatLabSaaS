using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class KiddingRecord : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int BreedingRecordId { get; set; }
    public BreedingRecord BreedingRecord { get; set; } = null!;

    public DateTime KiddingDate { get; set; }

    public int KidsBorn { get; set; }
    public int KidsAlive { get; set; }

    public KiddingOutcome Outcome { get; set; } = KiddingOutcome.Healthy;

    /// <summary>1 = easy, 5 = very difficult</summary>
    public int? DifficultyScore { get; set; }

    public AssistanceLevel AssistanceGiven { get; set; } = AssistanceLevel.None;

    public bool ColostrumGiven { get; set; }

    [MaxLength(500)]
    public string? DamStatus { get; set; }

    /// <summary>Link to kid goat profile if created (legacy — prefer Kids collection)</summary>
    public int? KidGoatId { get; set; }
    public Goat? KidGoat { get; set; }

    [MaxLength(2000)]
    public string? Complications { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public ICollection<Kid> Kids { get; set; } = new List<Kid>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
