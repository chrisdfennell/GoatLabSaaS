using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class Tenant
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Location { get; set; }

    public TenantUnits Units { get; set; } = TenantUnits.Imperial;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // --- Admin lifecycle ---

    /// <summary>Non-null when suspended — members can't sign in or select the tenant.</summary>
    public DateTime? SuspendedAt { get; set; }

    [MaxLength(500)]
    public string? SuspensionReason { get; set; }

    /// <summary>Non-null when soft-deleted — hidden from tenant picker and login.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Admin-only free-form notes — never exposed to tenant members.</summary>
    [MaxLength(4000)]
    public string? Notes { get; set; }

    /// <summary>Single admin-set label for grouping (e.g. "beta", "vip", "churn-risk").</summary>
    [MaxLength(50)]
    public string? Tag { get; set; }

    /// <summary>
    /// JSON object of boolean feature flags, e.g. {"betaMilkChart":true}. Keys are
    /// defined in <see cref="TenantFeatureFlags"/>. Absent key = flag off.
    /// </summary>
    [MaxLength(2000)]
    public string? FeatureFlagsJson { get; set; }

    // --- Billing ---

    // FK to Plan. Seeded plans: homestead (free), farm, dairy. Admin can add more.
    public int PlanId { get; set; }
    public Plan? Plan { get; set; }

    /// <summary>Stripe customer id (cus_...). Null until first Checkout session.</summary>
    [MaxLength(64)]
    public string? StripeCustomerId { get; set; }

    /// <summary>Stripe subscription id (sub_...). Null while on a free plan.</summary>
    [MaxLength(64)]
    public string? StripeSubscriptionId { get; set; }

    /// <summary>
    /// Mirrors Stripe subscription.status: trialing, active, past_due, canceled, etc.
    /// Null on free plans.
    /// </summary>
    [MaxLength(32)]
    public string? SubscriptionStatus { get; set; }

    /// <summary>UTC end of the current billing period; drives trial countdown + dunning banners.</summary>
    public DateTime? CurrentPeriodEnd { get; set; }

    public DateTime? TrialEndsAt { get; set; }

    // Set by TrialReminderJob when the reminder email for the current trial has
    // been sent. Cleared on TrialEndsAt change by the Stripe webhook so a
    // resubscription triggers a fresh reminder.
    public DateTime? TrialReminderSentAt { get; set; }

    // --- Alerts ---

    /// <summary>
    /// When true, AlertDigestJob emails the tenant owner once a day with a
    /// summary of new SmartAlerts. Independent of in-app alerts (always on if
    /// the plan has SmartAlerts) and push (independent feature). Defaults true.
    /// </summary>
    public bool AlertEmailEnabled { get; set; } = true;

    // --- Public profile ---

    /// <summary>
    /// When true, exposes a public goat-for-sale page per goat that has
    /// IsListedForSale=true. Owner opts in from /farm-settings. URL shape:
    /// /pub/{Slug}/{goatId}. Defaults false so existing tenants stay private.
    /// </summary>
    public bool PublicProfileEnabled { get; set; }

    [MaxLength(120)]
    public string? PublicContactEmail { get; set; }

    public ICollection<TenantMember> Members { get; set; } = new List<TenantMember>();
}

/// <summary>
/// Canonical list of admin-toggleable per-tenant feature flags. The admin UI
/// renders a switch for each key; absent/false means off.
/// </summary>
public static class TenantFeatureFlags
{
    public const string BetaMilkChart = "betaMilkChart";
    public const string AdvancedReports = "advancedReports";
    public const string BetaMobileApp = "betaMobileApp";

    public static readonly IReadOnlyList<(string Key, string Label, string Description)> All = new[]
    {
        (BetaMilkChart, "Beta milk chart", "Newer milk production chart with per-doe overlays."),
        (AdvancedReports, "Advanced reports", "Per-goat P&L, lactation curves, breeding summaries."),
        (BetaMobileApp, "Beta mobile app", "Unlock the mobile-only features (work in progress)."),
    };
}

public enum TenantUnits
{
    Imperial,
    Metric
}
