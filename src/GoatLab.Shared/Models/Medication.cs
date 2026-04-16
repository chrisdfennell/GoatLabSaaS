using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class Medication
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Dosage rate per unit of body weight (e.g. "1 mL per 10 lbs")</summary>
    [MaxLength(200)]
    public string? DosageRate { get; set; }

    /// <summary>mL per unit weight for calculator</summary>
    public double? DosagePerPound { get; set; }

    [MaxLength(100)]
    public string? Route { get; set; } // e.g. SubQ, IM, Oral, Topical

    /// <summary>Withdrawal period in days (meat/milk)</summary>
    public int? MeatWithdrawalDays { get; set; }
    public int? MilkWithdrawalDays { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
    public ICollection<MedicineCabinetItem> CabinetItems { get; set; } = new List<MedicineCabinetItem>();
}
