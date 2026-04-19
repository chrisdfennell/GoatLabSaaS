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

    [HttpGet("/breeds/{slug}")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Show(string slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug)) return NotFound();

        var raw = await _db.Goats.IgnoreQueryFilters()
            .Where(g => g.IsListedForSale && !g.IsExternal
                        && g.Tenant!.PublicProfileEnabled
                        && g.Tenant.DeletedAt == null
                        && g.Tenant.SuspendedAt == null
                        && g.Breed != null && g.Breed != "")
            .Select(g => new
            {
                g.Id,
                g.Breed,
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

        var matches = raw.Where(r => PublicController.BreedSlug(r.Breed) == slug).ToList();
        if (matches.Count == 0)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NotFoundBreed", slug);
        }

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

        // Use the most common original breed string as the human-readable name.
        var displayName = matches
            .GroupBy(m => m.Breed!.Trim())
            .OrderByDescending(g => g.Count())
            .First().Key;

        ViewData["DisplayName"] = displayName;
        ViewData["BreedSlug"] = slug;
        return View(farms);
    }
}
