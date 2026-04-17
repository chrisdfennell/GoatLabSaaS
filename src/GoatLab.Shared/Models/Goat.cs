using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class Goat : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? EarTag { get; set; }

    [MaxLength(100)]
    public string? Breed { get; set; }

    public Gender Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public GoatStatus Status { get; set; } = GoatStatus.Healthy;

    [MaxLength(2000)]
    public string? Bio { get; set; }

    [MaxLength(100)]
    public string? RegistrationNumber { get; set; }

    public GoatRegistry Registry { get; set; } = GoatRegistry.None;

    [MaxLength(50)]
    public string? TattooLeft { get; set; }

    [MaxLength(50)]
    public string? TattooRight { get; set; }

    [MaxLength(50)]
    public string? ScrapieTag { get; set; }

    [MaxLength(50)]
    public string? Microchip { get; set; }

    // True for "pedigree-only" ancestors that aren't part of your actual herd —
    // e.g. the sire/dam of a goat you purchased from another breeder. Filtered
    // from herd lists, maps, weigh-ins, etc., but available in pedigree pickers.
    public bool IsExternal { get; set; }

    [MaxLength(150)]
    public string? BreederName { get; set; }

    // Public listing — gated by Tenant.PublicProfileEnabled. When both are true,
    // the goat shows up at /pub/{tenantSlug}/{goatId} (anon-readable). Asking
    // price is in cents to match the rest of the codebase.
    public bool IsListedForSale { get; set; }
    public int? AskingPriceCents { get; set; }
    [MaxLength(2000)]
    public string? SaleNotes { get; set; }

    // Pedigree
    public int? SireId { get; set; }
    public Goat? Sire { get; set; }

    public int? DamId { get; set; }
    public Goat? Dam { get; set; }

    // Barn/Pen assignment
    public int? PenId { get; set; }
    public Pen? Pen { get; set; }

    // Navigation collections
    public ICollection<GoatPhoto> Photos { get; set; } = new List<GoatPhoto>();
    public ICollection<GoatDocument> Documents { get; set; } = new List<GoatDocument>();
    public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
    public ICollection<WeightRecord> WeightRecords { get; set; } = new List<WeightRecord>();
    public ICollection<FamachaScore> FamachaScores { get; set; } = new List<FamachaScore>();
    public ICollection<BodyConditionScore> BodyConditionScores { get; set; } = new List<BodyConditionScore>();
    public ICollection<MilkLog> MilkLogs { get; set; } = new List<MilkLog>();
    public ICollection<Goat> OffspringAsSire { get; set; } = new List<Goat>();
    public ICollection<Goat> OffspringAsDam { get; set; } = new List<Goat>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
