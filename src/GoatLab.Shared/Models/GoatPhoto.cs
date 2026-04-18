using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class GoatPhoto : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int GoatId { get; set; }
    public Goat? Goat { get; set; }

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Caption { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
