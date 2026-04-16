using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class BreedingRecord : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int DoeId { get; set; }
    public Goat Doe { get; set; } = null!;

    public int? BuckId { get; set; }
    public Goat? Buck { get; set; }

    public DateTime BreedingDate { get; set; }

    /// <summary>Estimated kidding date (~150 days gestation)</summary>
    public DateTime? EstimatedDueDate { get; set; }

    public BreedingOutcome Outcome { get; set; } = BreedingOutcome.Pending;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public ICollection<KiddingRecord> KiddingRecords { get; set; } = new List<KiddingRecord>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
