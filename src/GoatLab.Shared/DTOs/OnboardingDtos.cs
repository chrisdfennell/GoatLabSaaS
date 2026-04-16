namespace GoatLab.Shared.DTOs;

/// <summary>
/// Derived from real tenant data (no persistence). A step is Done when the
/// underlying record exists — users can't "dismiss" a step in Phase 1, which
/// keeps the schema clean; if that's too strict later, add a
/// TenantOnboardingDismissal table and OR it into the check.
/// </summary>
public record OnboardingStep(
    string Key,
    string Title,
    string Description,
    string Href,
    string Icon,
    bool Done
);

public record OnboardingStatus(
    IReadOnlyList<OnboardingStep> Steps,
    int DoneCount,
    int TotalCount,
    bool AllDone
);
