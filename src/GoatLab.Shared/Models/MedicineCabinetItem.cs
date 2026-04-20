using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class MedicineCabinetItem : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int MedicationId { get; set; }
    public Medication? Medication { get; set; }

    public double Quantity { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; } // mL, tablets, tubes, etc.

    public DateTime? ExpirationDate { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
