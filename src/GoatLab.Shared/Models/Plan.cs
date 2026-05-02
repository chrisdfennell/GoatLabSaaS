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

    // Marketplace upsells. Listing goats publicly stays free for every plan
    // (network effects — more listings = more buyers), but the premium
    // marketplace surfaces are paid:
    //   - MarketplaceMapPin: drop a farm pin on /marketplace's map view
    //   - StripeDeposits: collect online reservation deposits via Stripe
    // Per-plan listing volume is capped via Plan.MaxPublicListings, not a
    // feature flag, so the cap can vary by tier without enum churn.
    MarketplaceMapPin = 21,
    StripeDeposits = 22,

    // Dairy-only cosmetic upgrade — owners can pick an accent color and a
    // custom welcome message that render on /pub/{slug}. Lets serious
    // breeders make their farm page feel branded instead of generic-green.
    CustomBranding = 23,
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

    // Cap on simultaneously-public listings (IsListedForSale=true). Lets the
    // free Homestead tier participate in the marketplace (network effects)
    // without giving paid features away — paid plans set this to null.
    public int? MaxPublicListings { get; set; }

    // Cap on uploaded photos per individual goat. Listings with more photos
    // sell faster, so this is a real upgrade trigger. Null = unlimited.
    public int? MaxPhotosPerGoat { get; set; }

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
