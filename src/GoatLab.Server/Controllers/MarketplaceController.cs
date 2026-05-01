using System.Text.Json.Nodes;
using GoatLab.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Single SSR marketplace browse page — every active listing across every
// public farm. Filterable by breed, sex, state, price range; sortable by
// newest / price asc / price desc. Renders a goat-card grid + JSON-LD
// ItemList so search engines can index individual listings without crawling
// the breed directory tree.
//
// Same data shape as BreedsPagesController.Show but without the "must be in
// breed X" constraint, so this is the page to send buyers to when they want
// to browse everything.
[AllowAnonymous]
public class MarketplaceController : Controller
{
    private readonly GoatLabDbContext _db;
    public MarketplaceController(GoatLabDbContext db) => _db = db;

    private const int PageSize = 36;

    [HttpGet("/marketplace")]
    public async Task<IActionResult> Index(
        [FromQuery] string? breed,
        [FromQuery] string? sex,
        [FromQuery] int? minPrice,
        [FromQuery] int? maxPrice,
        [FromQuery] string? state,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;

        var anyFilter = !string.IsNullOrWhiteSpace(breed)
                     || !string.IsNullOrWhiteSpace(sex)
                     || minPrice.HasValue || maxPrice.HasValue
                     || !string.IsNullOrWhiteSpace(state)
                     || !string.IsNullOrWhiteSpace(sort);
        // 5-min cache when filters are active, 30 min on the bare page so the
        // hot landing path stays cheap.
        Response.Headers.CacheControl = anyFilter ? "public, max-age=300" : "public, max-age=1800";

        var raw = await _db.Goats.IgnoreQueryFilters()
            .Where(g => g.IsListedForSale && !g.IsExternal
                        && g.Tenant!.PublicProfileEnabled
                        && g.Tenant.DeletedAt == null
                        && g.Tenant.SuspendedAt == null)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.EarTag,
                g.Breed,
                g.Gender,
                g.DateOfBirth,
                g.AskingPriceCents,
                g.CreatedAt,
                g.ListedAt,
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

        IEnumerable<dynamic> filtered = raw;

        if (!string.IsNullOrWhiteSpace(breed))
        {
            var slug = breed.Trim();
            filtered = filtered.Where(m =>
                !string.IsNullOrEmpty((string?)m.Breed)
                && PublicController.BreedSlug((string)m.Breed) == slug);
        }
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

        // Default sort = newest-listed first. Falls back to CreatedAt for any
        // legacy row that slipped past the AddGoatListedAt backfill so the
        // sort never throws on a null ListedAt.
        filteredList = (sort?.ToLowerInvariant()) switch
        {
            "price-asc" => filteredList.OrderBy(m => m.AskingPriceCents ?? int.MaxValue).ThenBy(m => (string)m.Name).ToList(),
            "price-desc" => filteredList.OrderByDescending(m => m.AskingPriceCents ?? 0).ToList(),
            _ => filteredList.OrderByDescending(m => (DateTime?)m.ListedAt ?? (DateTime)m.CreatedAt).ToList(),
        };

        var totalCount = filteredList.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        if (page > totalPages) page = totalPages;

        var pageItems = filteredList
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(m => new BreedsPagesController.MarketplaceListing(
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
                string.IsNullOrEmpty((string?)m.PrimaryPhoto) ? null : "/" + (string)m.PrimaryPhoto,
                (DateTime?)m.ListedAt))
            .ToList();

        // Distinct breed pills for the filter UI — only show breeds with at
        // least one active listing, sorted by listing count.
        var breedFacets = raw
            .Where(r => !string.IsNullOrEmpty((string?)r.Breed))
            .GroupBy(r => PublicController.BreedSlug((string)r.Breed!))
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .Select(g => new BreedFacet(
                g.Key!,
                g.GroupBy(x => ((string)x.Breed!).Trim()).OrderByDescending(x => x.Count()).First().Key,
                g.Count()))
            .OrderByDescending(b => b.Count)
            .ThenBy(b => b.DisplayName)
            .Take(20)
            .ToList();

        ViewData["TotalCount"] = totalCount;
        ViewData["TotalPages"] = totalPages;
        ViewData["Page"] = page;
        ViewData["BreedFacets"] = breedFacets;
        ViewData["FilterBreed"] = breed;
        ViewData["FilterSex"] = sex;
        ViewData["FilterMinPrice"] = minPrice;
        ViewData["FilterMaxPrice"] = maxPrice;
        ViewData["FilterState"] = state;
        ViewData["FilterSort"] = sort;

        var origin = $"{Request.Scheme}://{Request.Host}";
        ViewData["JsonLd"] = BuildItemListJsonLd(pageItems, origin);
        ViewData["Title"] = anyFilter
            ? $"Goats for sale ({totalCount} listings) — GoatLab"
            : "Goats for sale — Browse the marketplace — GoatLab";
        ViewData["Description"] = "Browse goats for sale across every farm on GoatLab. Filter by breed, sex, state, and price. Verify pedigree across the network. Reserve with Stripe deposits.";

        return View(pageItems);
    }

    public record BreedFacet(string Slug, string DisplayName, int Count);

    // Anon JSON endpoint for the homepage "Just listed" preview strip. Cap of
    // 12 keeps payload small; 30s cache so the landing page isn't hammering
    // the DB on every visit.
    [HttpGet("/api/public/marketplace/recent")]
    public async Task<ActionResult<List<RecentListingDto>>> Recent(
        [FromQuery] int take = 6, CancellationToken ct = default)
    {
        if (take < 1) take = 1;
        if (take > 12) take = 12;

        Response.Headers.CacheControl = "public, max-age=30";

        var rows = await _db.Goats.IgnoreQueryFilters()
            .Where(g => g.IsListedForSale && !g.IsExternal
                        && g.Tenant!.PublicProfileEnabled
                        && g.Tenant.DeletedAt == null
                        && g.Tenant.SuspendedAt == null)
            .OrderByDescending(g => g.ListedAt ?? g.CreatedAt)
            .Take(take)
            .Select(g => new RecentListingDto(
                g.Id,
                g.Name,
                g.Breed,
                g.Gender.ToString(),
                g.AskingPriceCents,
                g.Tenant!.Slug,
                g.Tenant.Name,
                g.Tenant.Location,
                g.Photos
                    .OrderByDescending(p => p.IsPrimary)
                    .ThenBy(p => p.UploadedAt)
                    .Select(p => "/" + p.FilePath)
                    .FirstOrDefault(),
                g.ListedAt ?? g.CreatedAt))
            .ToListAsync(ct);

        return rows;
    }

    public record RecentListingDto(
        int Id, string Name, string? Breed, string Gender,
        int? AskingPriceCents, string FarmSlug, string FarmName, string? FarmLocation,
        string? PrimaryPhotoUrl, DateTime ListedAt);

    // Anon JSON endpoint feeding the /marketplace map toggle. Only returns
    // farms with both PublicLatitude AND PublicLongitude set — pins-only;
    // "list everywhere, pin where you can" is the UX. 5 min cache so
    // toggling map<->list isn't free DDoS bait.
    [HttpGet("/api/public/marketplace/farm-pins")]
    public async Task<ActionResult<List<FarmPinDto>>> FarmPins(CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "public, max-age=300";

        var rows = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.PublicProfileEnabled
                     && t.DeletedAt == null
                     && t.SuspendedAt == null
                     && t.PublicLatitude != null
                     && t.PublicLongitude != null)
            .Select(t => new
            {
                t.Slug,
                t.Name,
                t.Location,
                Lat = t.PublicLatitude!.Value,
                Lng = t.PublicLongitude!.Value,
                ListingCount = _db.Goats.IgnoreQueryFilters()
                    .Count(g => g.TenantId == t.Id && g.IsListedForSale && !g.IsExternal)
            })
            .Where(r => r.ListingCount > 0)
            .ToListAsync(ct);

        return rows
            .Select(r => new FarmPinDto(r.Slug, r.Name, r.Location, r.Lat, r.Lng, r.ListingCount))
            .ToList();
    }

    public record FarmPinDto(
        string Slug, string Name, string? Location,
        double Latitude, double Longitude, int ListingCount);

    private static string BuildItemListJsonLd(
        IReadOnlyList<BreedsPagesController.MarketplaceListing> items, string origin)
    {
        var list = new JsonArray();
        for (var i = 0; i < items.Count; i++)
        {
            var m = items[i];
            list.Add(new JsonObject
            {
                ["@type"] = "ListItem",
                ["position"] = i + 1,
                ["url"] = $"{origin}/pub/{m.FarmSlug}/{m.Id}",
                ["name"] = m.Name
            });
        }
        var doc = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "ItemList",
            ["name"] = "Goats for sale on GoatLab",
            ["numberOfItems"] = items.Count,
            ["itemListElement"] = list
        };
        return doc.ToJsonString();
    }
}
