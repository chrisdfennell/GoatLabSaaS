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
