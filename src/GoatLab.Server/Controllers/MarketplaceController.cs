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

        // Default sort = newest first — buyers expect "what's fresh" up top.
        // Price ascending for shoppers comparing on budget.
        filteredList = (sort?.ToLowerInvariant()) switch
        {
            "price-asc" => filteredList.OrderBy(m => m.AskingPriceCents ?? int.MaxValue).ThenBy(m => (string)m.Name).ToList(),
            "price-desc" => filteredList.OrderByDescending(m => m.AskingPriceCents ?? 0).ToList(),
            _ => filteredList.OrderByDescending(m => (DateTime)m.CreatedAt).ToList(),
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
                string.IsNullOrEmpty((string?)m.PrimaryPhoto) ? null : "/" + (string)m.PrimaryPhoto))
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
