using System.ComponentModel.DataAnnotations;
using GoatLab.Shared.Models;

namespace GoatLab.Shared.DTOs;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required, MaxLength(100)] string DisplayName,
    [Required, MaxLength(100)] string FarmName
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    bool RememberMe = false
);

public record SelectTenantRequest([Required] int TenantId);

public record CurrentUserDto(
    string Id,
    string Email,
    string DisplayName,
    int? CurrentTenantId,
    IReadOnlyList<TenantMembershipDto> Memberships,
    bool IsSuperAdmin = false,
    // Feature keys (AppFeature enum names) enabled on the current tenant's plan.
    // Empty when no tenant is selected. Super admins still receive the real list;
    // the UI can bypass to show everything if IsSuperAdmin.
    IReadOnlyList<string>? EnabledFeatures = null,
    // Current tenant's billing snapshot — lets the UI render trial countdowns
    // and past-due banners without a second request.
    BillingSnapshotDto? Billing = null
);

public record BillingSnapshotDto(
    string PlanName,
    string PlanSlug,
    string? SubscriptionStatus,
    DateTime? TrialEndsAt,
    DateTime? CurrentPeriodEnd);

public record TenantMembershipDto(
    int TenantId,
    string Name,
    string Slug,
    TenantRole Role
);
