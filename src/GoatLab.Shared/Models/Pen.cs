using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class Pen : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int BarnId { get; set; }
    public Barn Barn { get; set; } = null!;

    public int Capacity { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public ICollection<Goat> Goats { get; set; } = new List<Goat>();
}
