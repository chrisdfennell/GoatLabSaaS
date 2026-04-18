using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class MedicalRecord : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int GoatId { get; set; }
    public Goat Goat { get; set; } = null!;

    public MedicalRecordType RecordType { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public DateTime Date { get; set; }

    // Medication used (optional link)
    public int? MedicationId { get; set; }
    public Medication? Medication { get; set; }

    [MaxLength(100)]
    public string? Dosage { get; set; }

    [MaxLength(200)]
    public string? AdministeredBy { get; set; }

    // Recurring schedule
    public RecurrenceInterval Recurrence { get; set; } = RecurrenceInterval.None;
    public DateTime? NextDueDate { get; set; }

    // Copied from Medication.MilkWithdrawalDays / MeatWithdrawalDays at save time —
    // snapshotting means later edits to the Medication's withdrawal policy don't
    // retroactively shift history or clear an active hold.
    public DateTime? MilkWithdrawalEndsAt { get; set; }
    public DateTime? MeatWithdrawalEndsAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
