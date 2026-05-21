namespace GoatLab.Server.Models;

// View model for the server-rendered marketing landing page (Views/Home/Landing.cshtml).
// Everything the page shows is materialized on the server so crawlers and
// non-JS agents get the full value prop + real pricing in the initial HTML —
// no WASM execution required. See HomeController.Index (SaaS branch).
public sealed class LandingPageModel
{
    public IReadOnlyList<LandingPlan> Plans { get; init; } = [];
    public IReadOnlyList<LandingListing> RecentListings { get; init; } = [];
}

public sealed record LandingPlan(
    string Name,
    string? Description,
    int PriceMonthlyCents,
    int TrialDays,
    int? MaxGoats,
    int? MaxUsers,
    IReadOnlyList<string> Features);

public sealed record LandingListing(
    int Id,
    string Name,
    string? Breed,
    string Gender,
    int? AskingPriceCents,
    string FarmSlug,
    string FarmName,
    string? FarmLocation,
    string? PrimaryPhotoUrl,
    DateTime ListedAt);
