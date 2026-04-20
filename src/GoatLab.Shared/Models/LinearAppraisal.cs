using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class LinearAppraisal : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int GoatId { get; set; }
    public Goat? Goat { get; set; }

    public DateTime AppraisalDate { get; set; }

    [MaxLength(150)]
    public string? Appraiser { get; set; }

    // Dairy goat LA system uses four category scores (0-100 each) and a final composite.
    public int? GeneralAppearance { get; set; }
    public int? DairyCharacter { get; set; }
    public int? BodyCapacity { get; set; }
    public int? MammarySystem { get; set; }

    /// <summary>Final composite score (0-100).</summary>
    public int? FinalScore { get; set; }

    public LinearAppraisalClassification? Classification { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
