using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

// Every feature a plan can enable/disable. Adding a new feature requires a
// code deploy — the admin UI renders a toggle per enum value. Do not renumber
// existing values; the integer is what's stored on PlanFeature.
public enum AppFeature
{
    Goats = 0,
    Health = 1,
    Breeding = 2,
    Milk = 3,
    Sales = 4,
    Finance = 5,
    Inventory = 6,
    Calendar = 7,
    Map = 8,
    CareGuide = 9,
    Barns = 10,
    AdvancedReports = 11,
    ShowRecords = 12,
    DataExport = 13,
    SmartAlerts = 14,
    PushNotifications = 15,
    PdfDocuments = 16,
    CoiCalculator = 17,
    Forecasting = 18,
    BuyerWaitlist = 19,
    WebhooksAndApi = 20,
}

public class Plan
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    // URL-safe identifier used in API paths and on the landing page.
    [Required, MaxLength(64)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    // Display price in cents (e.g., 1900 = $19.00). Actual billing runs
    // through Stripe against StripePriceId.
    public int PriceMonthlyCents { get; set; }

    // Paid plans only. Null for free/internal plans.
    [MaxLength(64)]
    public string? StripePriceId { get; set; }

    public int TrialDays { get; set; }

    // Null = unlimited. Enforced by FeatureGate.CanAddGoat/UserAsync.
    public int? MaxGoats { get; set; }
    public int? MaxUsers { get; set; }

    // IsPublic controls pricing page visibility; IsActive blocks new
    // subscriptions without affecting existing subscribers.
    public bool IsPublic { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PlanFeature> Features { get; set; } = new List<PlanFeature>();
}

public class PlanFeature
{
    public int PlanId { get; set; }
    public Plan? Plan { get; set; }

    public AppFeature Feature { get; set; }

    public bool Enabled { get; set; }
}
