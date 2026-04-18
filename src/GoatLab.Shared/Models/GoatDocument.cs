using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class GoatDocument : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int GoatId { get; set; }
    public Goat? Goat { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? DocumentType { get; set; } // e.g. "Vet Record", "Registration"

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
