using System.Text.Json.Nodes;
using GoatLab.Server.Data;
using GoatLab.Server.Services.Billing;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Server-side Razor pages for the public farm + goat-for-sale routes. These
// replace the Blazor WASM versions at /pub/{slug} and /pub/{slug}/{goatId} so
// search engines (Googlebot, Bingbot, social-media unfurlers) see real HTML
// with content + OpenGraph + JSON-LD Product structured data on first request.
//
// Why this matters: the breed directory + transfer flow + waitlist deposits
// are all built around traffic from search. Crawlers can't run Blazor WASM,
// so they were seeing an empty shell on every public listing. SSR fixes that
// for the highest-conversion pages on the site.
//
// The deposit-reservation flow uses a plain HTML form that POSTs back here
// and 303-redirects to Stripe Checkout — no JS required, identical to the
// existing JSON path at /api/public/farms/{slug}/goats/{id}/reserve.
[AllowAnonymous]
public class PublicFarmPagesController : Controller
{
    private readonly GoatLabDbContext _db;
    private readonly IBillingService _billing;

    public PublicFarmPagesController(GoatLabDbContext db, IBillingService billing)
    {
        _db = db;
        _billing = billing;
    }

    [HttpGet("/pub/{slug}")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Index(string slug, CancellationToken ct)
    {
        var tenant = await GetPublicTenantAsync(slug, ct);
        if (tenant is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NotFoundFarm", slug);
        }

        // Same projection as PublicController.List — keep in sync.
        var goats = await _db.Goats.IgnoreQueryFilters()
            .Where(g => g.TenantId == tenant.Id && g.IsListedForSale && !g.IsExternal)
            .OrderBy(g => g.Name)
            .Select(g => new PublicGoatListItemDto(
                g.Id, g.Name, g.EarTag, g.Breed, g.Gender.ToString(),
                g.DateOfBirth, g.AskingPriceCents,
                g.Photos.OrderByDescending(p => p.IsPrimary)
                    .ThenBy(p => p.UploadedAt)
                    .Select(p => "/" + p.FilePath)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        var origin = $"{Request.Scheme}://{Request.Host}";
        var canonical = $"{origin}/pub/{tenant.Slug}";

        ViewData["FarmSlug"] = tenant.Slug;
        ViewData["FarmName"] = tenant.Name;
        ViewData["FarmLocation"] = tenant.Location;
        ViewData["ContactEmail"] = tenant.PublicContactEmail;
        ViewData["Canonical"] = canonical;
        ViewData["Title"] = $"{tenant.Name} — Goats for sale on GoatLab";
        ViewData["Description"] = goats.Count switch
        {
            0 => $"{tenant.Name} — public farm page on GoatLab. No animals currently listed for sale.",
            1 => $"{tenant.Name} has 1 goat listed for sale on GoatLab.",
            _ => $"{tenant.Name} has {goats.Count} goats listed for sale on GoatLab — pedigrees, photos, prices, and contact details."
        };

        // OG image: first listing with a primary photo wins.
        var ogImage = goats.Where(g => !string.IsNullOrEmpty(g.PrimaryPhotoUrl))
                           .Select(g => origin + g.PrimaryPhotoUrl)
                           .FirstOrDefault();
        if (!string.IsNullOrEmpty(ogImage)) ViewData["OgImage"] = ogImage;

        ViewData["JsonLd"] = BuildFarmJsonLd(tenant, goats, origin, canonical);
        return View(goats);
    }

    [HttpGet("/pub/{slug}/{goatId:int}")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Show(string slug, int goatId, [FromQuery] string? deposit, CancellationToken ct)
    {
        var tenant = await GetPublicTenantAsync(slug, ct);
        if (tenant is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NotFoundFarm", slug);
        }

        var goat = await _db.Goats.IgnoreQueryFilters()
            .Include(g => g.Photos)
            .Include(g => g.Sire).ThenInclude(s => s!.Sire)
            .Include(g => g.Sire).ThenInclude(s => s!.Dam)
            .Include(g => g.Dam).ThenInclude(d => d!.Sire)
            .Include(g => g.Dam).ThenInclude(d => d!.Dam)
            .FirstOrDefaultAsync(g => g.Id == goatId && g.TenantId == tenant.Id
                                      && g.IsListedForSale && !g.IsExternal, ct);
        if (goat is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NotFoundGoat");
        }

        var depositCents = ComputeDepositCents(tenant, goat);

        var dto = new PublicGoatDto(
            goat.Id, goat.Name, goat.EarTag, goat.Breed, goat.Gender.ToString(),
            goat.DateOfBirth, goat.Bio,
            goat.Registry == GoatRegistry.None ? null : goat.Registry.ToString(),
            goat.RegistrationNumber, goat.BreederName,
            goat.AskingPriceCents, goat.SaleNotes,
            tenant.Name, tenant.Slug, tenant.Location, tenant.PublicContactEmail,
            goat.Photos.OrderByDescending(p => p.IsPrimary)
                .ThenBy(p => p.UploadedAt)
                .Select(p => new PublicGoatPhotoDto("/" + p.FilePath, p.Caption, p.IsPrimary))
                .ToList(),
            BuildPedigree(goat),
            depositCents);

        var origin = $"{Request.Scheme}://{Request.Host}";
        var canonical = $"{origin}/pub/{tenant.Slug}/{goat.Id}";

        var ageBit = goat.DateOfBirth.HasValue ? $", {AgeString(goat.DateOfBirth.Value)}" : "";
        var priceBit = goat.AskingPriceCents.HasValue
            ? $" — ${(goat.AskingPriceCents.Value / 100m):N0}"
            : "";

        ViewData["FarmSlug"] = tenant.Slug;
        ViewData["DepositStatus"] = string.IsNullOrEmpty(deposit) ? null : deposit;
        ViewData["Canonical"] = canonical;
        ViewData["Title"] = $"{goat.Name} — {goat.Breed ?? "goat"} for sale at {tenant.Name}";
        ViewData["Description"] = $"{goat.Name}{ageBit} {goat.Gender}{priceBit}. Listed for sale by {tenant.Name}" +
            (string.IsNullOrEmpty(tenant.Location) ? "" : $" in {tenant.Location}") +
            ". Pedigree, photos, and contact details on GoatLab.";

        var primaryPhoto = dto.Photos.FirstOrDefault(p => p.IsPrimary) ?? dto.Photos.FirstOrDefault();
        if (primaryPhoto is not null) ViewData["OgImage"] = origin + primaryPhoto.Url;

        ViewData["JsonLd"] = BuildGoatJsonLd(dto, tenant, origin, canonical);
        return View(dto);
    }

    public record ReserveForm(string BuyerEmail, string BuyerName, string? BuyerPhone, string? Notes);

    [HttpPost("/pub/{slug}/{goatId:int}/reserve-form")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reserve(string slug, int goatId, ReserveForm form, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(form.BuyerEmail) || string.IsNullOrWhiteSpace(form.BuyerName))
        {
            TempData["ReserveError"] = "Email and name are required.";
            return RedirectToAction(nameof(Show), new { slug, goatId });
        }

        var tenant = await GetPublicTenantAsync(slug, ct);
        if (tenant is null) return NotFound();

        var goat = await _db.Goats.IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Id == goatId && g.TenantId == tenant.Id && g.IsListedForSale, ct);
        if (goat is null) return NotFound();

        var depositCents = ComputeDepositCents(tenant, goat);
        if (depositCents is null or <= 0)
        {
            TempData["ReserveError"] = "This farm is not accepting online deposits for this listing.";
            return RedirectToAction(nameof(Show), new { slug, goatId });
        }

        var origin = $"{Request.Scheme}://{Request.Host}";
        var req = new PublicReservationRequest(form.BuyerEmail, form.BuyerName, form.BuyerPhone, form.Notes);

        var url = await _billing.CreateDepositCheckoutSessionAsync(
            tenant, goat, depositCents.Value, req, origin, ct);

        // 303 See Other so a browser cleanly switches from POST to GET when
        // following the redirect to Stripe Checkout.
        return Redirect(url);
    }

    private async Task<Tenant?> GetPublicTenantAsync(string slug, CancellationToken ct) =>
        await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == slug
                                      && t.PublicProfileEnabled
                                      && t.DeletedAt == null
                                      && t.SuspendedAt == null, ct);

    private static int? ComputeDepositCents(Tenant tenant, Goat goat)
    {
        if (tenant.PublicDepositPercent <= 0) return null;
        if (goat.AskingPriceCents is not int price || price <= 0) return null;
        var raw = price * tenant.PublicDepositPercent / 100;
        return Math.Max(raw, 50); // Stripe minimum
    }

    private static PublicPedigreeNodeDto? BuildPedigree(Goat g)
    {
        if (g.Sire is null && g.Dam is null) return null;
        return new PublicPedigreeNodeDto(
            g.Id, g.Name, g.RegistrationNumber, g.Breed,
            BuildNode(g.Sire), BuildNode(g.Dam));
    }

    private static PublicPedigreeNodeDto? BuildNode(Goat? g)
    {
        if (g is null) return null;
        return new PublicPedigreeNodeDto(
            g.Id, g.Name, g.RegistrationNumber, g.Breed,
            g.Sire is null ? null : new PublicPedigreeNodeDto(g.Sire.Id, g.Sire.Name, g.Sire.RegistrationNumber, g.Sire.Breed, null, null),
            g.Dam is null ? null : new PublicPedigreeNodeDto(g.Dam.Id, g.Dam.Name, g.Dam.RegistrationNumber, g.Dam.Breed, null, null));
    }

    // JSON-LD lives in the controller, not the view. C#'s `@type` (verbatim
    // identifier) clashes with Razor's `@` transition character if you build
    // schema.org objects via anonymous-type literals inside .cshtml. JsonObject
    // sidesteps that and keeps the views purely presentational.
    public static string BuildFarmJsonLd(Tenant tenant, IReadOnlyList<PublicGoatListItemDto> goats, string origin, string canonical)
    {
        var farmUrl = $"{origin}/pub/{tenant.Slug}";

        var organization = new JsonObject
        {
            ["@type"] = "Organization",
            ["name"] = tenant.Name,
            ["url"] = farmUrl,
        };
        if (!string.IsNullOrEmpty(tenant.Location))
        {
            organization["address"] = new JsonObject
            {
                ["@type"] = "PostalAddress",
                ["addressLocality"] = tenant.Location,
            };
        }
        if (!string.IsNullOrEmpty(tenant.PublicContactEmail))
        {
            organization["email"] = tenant.PublicContactEmail;
        }

        var items = new JsonArray();
        for (var i = 0; i < goats.Count; i++)
        {
            var g = goats[i];
            var product = new JsonObject
            {
                ["@type"] = "Product",
                ["name"] = g.Name,
                ["url"] = $"{origin}/pub/{tenant.Slug}/{g.Id}",
            };
            if (!string.IsNullOrEmpty(g.PrimaryPhotoUrl))
                product["image"] = origin + g.PrimaryPhotoUrl;
            if (!string.IsNullOrEmpty(g.Breed))
                product["category"] = g.Breed;
            if (g.AskingPriceCents.HasValue)
            {
                product["offers"] = new JsonObject
                {
                    ["@type"] = "Offer",
                    ["price"] = (g.AskingPriceCents.Value / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    ["priceCurrency"] = "USD",
                    ["availability"] = "https://schema.org/InStock",
                    ["url"] = $"{origin}/pub/{tenant.Slug}/{g.Id}",
                };
            }
            items.Add(new JsonObject
            {
                ["@type"] = "ListItem",
                ["position"] = i + 1,
                ["item"] = product,
            });
        }

        var doc = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "CollectionPage",
            ["name"] = $"{tenant.Name} — Goats for sale",
            ["url"] = canonical,
            ["about"] = organization,
            ["mainEntity"] = new JsonObject
            {
                ["@type"] = "ItemList",
                ["numberOfItems"] = goats.Count,
                ["itemListElement"] = items,
            },
        };
        return doc.ToJsonString();
    }

    public static string BuildGoatJsonLd(PublicGoatDto goat, Tenant tenant, string origin, string canonical)
    {
        var farmUrl = $"{origin}/pub/{tenant.Slug}";

        var product = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = goat.Name,
            ["url"] = canonical,
            ["sku"] = goat.RegistrationNumber ?? goat.EarTag ?? goat.Id.ToString(),
            ["brand"] = new JsonObject
            {
                ["@type"] = "Organization",
                ["name"] = goat.FarmName,
                ["url"] = farmUrl,
            },
        };
        var description = string.IsNullOrEmpty(goat.Bio) ? goat.SaleNotes : goat.Bio;
        if (!string.IsNullOrEmpty(description))
            product["description"] = description;
        if (!string.IsNullOrEmpty(goat.Breed))
            product["category"] = goat.Breed;

        var images = new JsonArray();
        foreach (var p in goat.Photos.Take(5))
            images.Add(origin + p.Url);
        if (images.Count > 0)
            product["image"] = images;

        if (goat.AskingPriceCents.HasValue)
        {
            var seller = new JsonObject
            {
                ["@type"] = "Organization",
                ["name"] = goat.FarmName,
            };
            if (!string.IsNullOrEmpty(goat.FarmContactEmail))
                seller["email"] = goat.FarmContactEmail;

            product["offers"] = new JsonObject
            {
                ["@type"] = "Offer",
                ["price"] = (goat.AskingPriceCents.Value / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                ["priceCurrency"] = "USD",
                ["availability"] = "https://schema.org/InStock",
                ["url"] = canonical,
                ["seller"] = seller,
            };
        }

        var crumbs = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = new JsonArray
            {
                new JsonObject { ["@type"] = "ListItem", ["position"] = 1, ["name"] = "Browse breeders", ["item"] = $"{origin}/breeds" },
                new JsonObject { ["@type"] = "ListItem", ["position"] = 2, ["name"] = goat.FarmName, ["item"] = farmUrl },
                new JsonObject { ["@type"] = "ListItem", ["position"] = 3, ["name"] = goat.Name, ["item"] = canonical },
            },
        };

        // Two top-level documents in one <script> block — schema.org allows an
        // array at the document root.
        var arr = new JsonArray { product, crumbs };
        return arr.ToJsonString();
    }

    public static string AgeString(DateTime dob)
    {
        var months = (int)((DateTime.UtcNow - dob).TotalDays / 30.44);
        if (months < 12) return months + " mo";
        var years = months / 12;
        var rem = months % 12;
        return rem == 0 ? years + " yr" : $"{years} yr {rem} mo";
    }
}
