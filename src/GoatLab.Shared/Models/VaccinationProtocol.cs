using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

/// <summary>
/// A reusable schedule of medical doses (vaccines, dewormers, boosters) that can be
/// applied to a goat to auto-generate the matching MedicalRecords with NextDueDate
/// offsets baked in.
/// </summary>
public class VaccinationProtocol : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>"Doe", "Buck", "Kid", or null = any goat.</summary>
    [MaxLength(50)]
    public string? AppliesTo { get; set; }

    /// <summary>True for the seeded built-in templates so users know not to delete them blindly.</summary>
    public bool IsBuiltIn { get; set; }

    public ICollection<ProtocolDose> Doses { get; set; } = new List<ProtocolDose>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ProtocolDose : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int VaccinationProtocolId { get; set; }
    public VaccinationProtocol VaccinationProtocol { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    public MedicalRecordType RecordType { get; set; } = MedicalRecordType.Vaccination;

    public int? MedicationId { get; set; }
    public Medication? Medication { get; set; }

    /// <summary>Days after the protocol "apply" date that this dose should fire.</summary>
    public int DayOffset { get; set; }

    /// <summary>Recurrence after the first dose (e.g. Annually for CDT booster).</summary>
    public RecurrenceInterval Recurrence { get; set; } = RecurrenceInterval.None;

    public int SortOrder { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
