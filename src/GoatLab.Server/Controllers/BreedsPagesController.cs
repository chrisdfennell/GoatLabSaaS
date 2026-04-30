using GoatLab.Server.Data;
using GoatLab.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Server-side Razor pages for the breed directory. These replace the Blazor
// WASM versions that lived at /breeds and /breeds/{slug} — crawlers (Googlebot,
// Bingbot, social-media unfurlers) see real HTML with content, not an empty
// Blazor shell. Humans also benefit from faster first paint (no WASM download).
//
// Routes are explicit via [HttpGet] attributes, which match in the MVC endpoint
// pipeline before MapFallbackToFile punts unmatched paths to the Blazor index.
[AllowAnonymous]
public class BreedsPagesController : Controller
{
    private readonly GoatLabDbContext _db;
    public BreedsPagesController(GoatLabDbContext db) => _db = db;

    [HttpGet("/breeds")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Same aggregation as /api/public/breeds — keep it in sync if the API
        // one changes. We inline here to avoid an HTTP round-trip.
        var raw = await _db.Goats.IgnoreQueryFilters()
            .Where(g => g.IsListedForSale && !g.IsExternal
                        && g.Tenant!.PublicProfileEnabled
                        && g.Tenant.DeletedAt == null
                        && g.Tenant.SuspendedAt == null
                        && g.Breed != null && g.Breed != "")
            .Select(g => new { g.Breed, g.TenantId })
            .ToListAsync(ct);

        var breeds = raw
            .GroupBy(r => PublicController.BreedSlug(r.Breed))
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .Select(g => new BreedSummaryDto(
                g.Key!,
                g.GroupBy(r => r.Breed!.Trim())
                    .OrderByDescending(x => x.Count())
                    .First().Key,
                g.Select(r => r.TenantId).Distinct().Count(),
                g.Count()))
            .OrderByDescending(b => b.ListingCount)
            .ThenBy(b => b.DisplayName)
            .ToList();

        return View(breeds);
    }

    // Filterable by sex / price / state via querystring. Renders a goat grid
    // (each goat clickable to its public listing) plus a farm-summary sidebar.
    // Cache window drops from 1 hour to 5 minutes when any filter is active —
    // filtered combos are too varied to cache aggressively.
    [HttpGet("/breeds/{slug}")]
    public async Task<IActionResult> Show(
        string slug,
        [FromQuery] string? sex,
        [FromQuery] int? maxPrice,
        [FromQuery] int? minPrice,
        [FromQuery] string? state,
        [FromQuery] string? sort,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return NotFound();

        var anyFilter = !string.IsNullOrWhiteSpace(sex)
                     || maxPrice.HasValue || minPrice.HasValue
                     || !string.IsNullOrWhiteSpace(state)
                     || !string.IsNullOrWhiteSpace(sort);
        Response.Headers.CacheControl = anyFilter ? "public, max-age=300" : "public, max-age=3600";

        // Pull individual goats so we can both render a flat marketplace grid
        // AND aggregate to farm cards. EF projection keeps payload small.
        var raw = await _db.Goats.IgnoreQueryFilters()
            .Where(g => g.IsListedForSale && !g.IsExternal
                        && g.Tenant!.PublicProfileEnabled
                        && g.Tenant.DeletedAt == null
                        && g.Tenant.SuspendedAt == null
                        && g.Breed != null && g.Breed != "")
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.EarTag,
                g.Breed,
                g.Gender,
                g.DateOfBirth,
                g.AskingPriceCents,
                TenantId = g.TenantId,
                TenantSlug = g.Tenant!.Slug,
                TenantName = g.Tenant.Name,
                TenantLocation = g.Tenant.Location,
                PrimaryPhoto = g.Photos
                    .OrderByDescending(p => p.IsPrimary)
                    .ThenBy(p => p.UploadedAt)
                    .Select(p => p.FilePath)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        // Slug match must happen client-side because BreedSlug is a static C#
        // method (can't translate to SQL). Cheap — listings count is bounded.
        var matches = raw.Where(r => PublicController.BreedSlug(r.Breed) == slug).ToList();
        if (matches.Count == 0)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NotFoundBreed", slug);
        }

        // Apply filters. State match is substring/case-insensitive against
        // Tenant.Location — good enough until we add a structured State field.
        IEnumerable<dynamic> filtered = matches;
        if (!string.IsNullOrWhiteSpace(sex))
            filtered = filtered.Where(m => string.Equals(m.Gender.ToString(), sex, StringComparison.OrdinalIgnoreCase));
        if (minPrice.HasValue)
            filtered = filtered.Where(m => m.AskingPriceCents.HasValue && m.AskingPriceCents.Value >= minPrice.Value * 100);
        if (maxPrice.HasValue)
            filtered = filtered.Where(m => m.AskingPriceCents.HasValue && m.AskingPriceCents.Value <= maxPrice.Value * 100);
        if (!string.IsNullOrWhiteSpace(state))
        {
            var s = state.Trim();
            filtered = filtered.Where(m => !string.IsNullOrEmpty((string?)m.TenantLocation)
                                        && ((string)m.TenantLocation).Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        var filteredList = filtered.ToList();

        // Sort. Default = price asc with nulls last so cheapest legitimate
        // listings come first. "newest" uses Id as a proxy for create time
        // since per-listing timestamps aren't projected.
        filteredList = (sort?.ToLowerInvariant()) switch
        {
            "price-desc" => filteredList.OrderByDescending(m => m.AskingPriceCents ?? 0).ToList(),
            "newest" => filteredList.OrderByDescending(m => (int)m.Id).ToList(),
            _ => filteredList.OrderBy(m => m.AskingPriceCents ?? int.MaxValue).ThenBy(m => (string)m.Name).ToList(),
        };

        // Goat cards for the marketplace grid.
        var listings = filteredList.Select(m => new MarketplaceListing(
            (int)m.Id,
            (string)m.Name,
            (string?)m.EarTag,
            (string?)m.Breed,
            m.Gender.ToString(),
            (DateTime?)m.DateOfBirth,
            (int?)m.AskingPriceCents,
            (string)m.TenantSlug,
            (string)m.TenantName,
            (string?)m.TenantLocation,
            string.IsNullOrEmpty((string?)m.PrimaryPhoto) ? null : "/" + (string)m.PrimaryPhoto)).ToList();

        // Farm summary still derived from the unfiltered match set so users
        // can see "this breed is available at 12 farms, you're seeing 3 after
        // your filters." Helpful trust + scope signal.
        var farms = matches
            .GroupBy(r => new { r.TenantId, r.TenantSlug, r.TenantName, r.TenantLocation })
            .Select(g => new BreedFarmDto(
                g.Key.TenantSlug,
                g.Key.TenantName,
                g.Key.TenantLocation,
                g.Count(),
                g.Where(x => x.AskingPriceCents.HasValue).Select(x => x.AskingPriceCents!.Value).DefaultIfEmpty(0).Min() switch
                {
                    0 => (int?)null,
                    var min => min
                },
                g.Where(x => !string.IsNullOrEmpty(x.PrimaryPhoto))
                    .Select(x => "/" + x.PrimaryPhoto)
                    .FirstOrDefault()))
            .OrderByDescending(f => f.ListingCount)
            .ThenBy(f => f.FarmName)
            .ToList();

        var displayName = matches
            .GroupBy(m => m.Breed!.Trim())
            .OrderByDescending(g => g.Count())
            .First().Key;

        ViewData["DisplayName"] = displayName;
        ViewData["BreedSlug"] = slug;
        ViewData["Farms"] = farms;
        ViewData["TotalListings"] = matches.Count;
        ViewData["FilterSex"] = sex;
        ViewData["FilterMaxPrice"] = maxPrice;
        ViewData["FilterMinPrice"] = minPrice;
        ViewData["FilterState"] = state;
        ViewData["FilterSort"] = sort;
        return View(listings);
    }

    public record MarketplaceListing(
        int Id,
        string Name,
        string? EarTag,
        string? Breed,
        string Gender,
        DateTime? DateOfBirth,
        int? AskingPriceCents,
        string FarmSlug,
        string FarmName,
        string? FarmLocation,
        string? PrimaryPhotoUrl);
}
