using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public enum AlertType
{
    MedicationOverdue = 0,
    MedicationDue = 1,
    KiddingOverdue = 2,
    KiddingUpcoming = 3,
    LowFeedStock = 4,
    WeightDrop = 5,
}

public enum AlertSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
}

public class Alert : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Body { get; set; }

    // Optional pointer to the source record so the scanner can de-dupe
    // and the UI can deep-link. EntityType matches the EF entity name
    // (e.g. "MedicalRecord", "BreedingRecord", "FeedInventory", "Goat").
    [MaxLength(64)]
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }

    [MaxLength(300)]
    public string? DeepLink { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public DateTime? DismissedAt { get; set; }
}
