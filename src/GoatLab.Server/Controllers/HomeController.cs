using System.Text;
using System.Text.Json.Nodes;
using GoatLab.Server.Data;
using GoatLab.Server.Models;
using GoatLab.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// The "/" route is mode-aware:
//   - SaaS:  server-render the marketing landing page (Views/Home/Landing.cshtml)
//            so crawlers and AI agents see the full pitch + real pricing in the
//            initial HTML. Previously this served the Blazor WASM shell, which
//            non-JS agents saw as a perpetual spinner. Authenticated users skip
//            the pitch and go straight to the dashboard.
//   - OSS:   render Views/Home/OssWelcome.cshtml — a tiny SSR welcome page
//            with sign-in / register CTAs. The SaaS marketing landing has
//            marketplace links and a "across every farm" pitch that makes no
//            sense for a self-hoster who already chose to self-host.
[AllowAnonymous]
public sealed class HomeController : Controller
{
    private readonly IAppMode _mode;
    private readonly GoatLabDbContext _db;

    public HomeController(IAppMode mode, GoatLabDbContext db)
    {
        _mode = mode;
        _db = db;
    }

    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Authed users (either mode) land straight on the dashboard rather than
        // the marketing/welcome page.
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect("/dashboard");
        }

        if (_mode.IsOss)
        {
            return View("OssWelcome");
        }

        var plans = await _db.Plans
            .Where(p => p.IsPublic && p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .Include(p => p.Features)
            .AsNoTracking()
            .Select(p => new LandingPlan(
                p.Name,
                p.Description,
                p.PriceMonthlyCents,
                p.TrialDays,
                p.MaxGoats,
                p.MaxUsers,
                p.Features.Where(f => f.Enabled)
                    .Select(f => f.Feature.ToString())
                    .ToList()))
            .ToListAsync(ct);

        // Humanize feature keys ("ShowRecords" -> "Show records") server-side so
        // the view stays presentational.
        var humanized = plans
            .Select(p => p with { Features = p.Features.Select(FeatureLabel).ToList() })
            .ToList();

        // Mirror MarketplaceController.Recent so the "Just listed" strip is in
        // the HTML, not fetched by JS after load.
        var recent = await _db.Goats.IgnoreQueryFilters()
            .Where(g => g.IsListedForSale && !g.IsExternal
                        && g.Tenant!.PublicProfileEnabled
                        && g.Tenant.DeletedAt == null
                        && g.Tenant.SuspendedAt == null)
            .OrderByDescending(g => g.ListedAt ?? g.CreatedAt)
            .Take(6)
            .Select(g => new LandingListing(
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

        ViewData["Title"] = "GoatLab — Herd management + buyer marketplace for goat farmers";
        ViewData["Description"] = "GoatLab is the all-in-one platform for goat farmers — herd records, "
            + "health, breeding, milk, finances, AND a public marketplace where buyers find you, reserve "
            + "goats with Stripe deposits, and verify pedigree across every farm on the network. Works offline.";
        ViewData["JsonLd"] = BuildJsonLd(humanized, $"{Request.Scheme}://{Request.Host}");

        // Cache the anonymous marketing render only (the authed redirect above
        // returns before this and stays uncached).
        Response.Headers.CacheControl = "public, max-age=300";

        return View("Landing", new LandingPageModel
        {
            Plans = humanized,
            RecentListings = recent,
        });
    }

    // "ShowRecords" -> "Show records"
    private static string FeatureLabel(string feature)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < feature.Length; i++)
        {
            if (i > 0 && char.IsUpper(feature[i])) sb.Append(' ');
            sb.Append(i == 0 ? feature[i] : char.ToLowerInvariant(feature[i]));
        }
        return sb.ToString();
    }

    // schema.org SoftwareApplication with one Offer per public plan, plus the
    // publishing Organization. Built here (not in the view) because '@type' /
    // '@context' collide with Razor's transition character in .cshtml literals.
    private static string BuildJsonLd(IReadOnlyList<LandingPlan> plans, string origin)
    {
        var offers = new JsonArray();
        foreach (var p in plans)
        {
            offers.Add(new JsonObject
            {
                ["@type"] = "Offer",
                ["name"] = p.Name,
                ["price"] = (p.PriceMonthlyCents / 100m).ToString("0.00"),
                ["priceCurrency"] = "USD",
                ["category"] = p.PriceMonthlyCents == 0 ? "free" : "subscription",
            });
        }

        var root = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "SoftwareApplication",
            ["name"] = "GoatLab",
            ["applicationCategory"] = "BusinessApplication",
            ["operatingSystem"] = "Web, iOS, Android",
            ["url"] = origin + "/",
            ["description"] = "Goat herd management plus a public buyer marketplace — health, breeding, "
                + "milk, finances, public farm pages with Stripe deposits, cross-farm pedigree verification, "
                + "vet share-links, and an offline-first PWA.",
            ["offers"] = offers,
            ["publisher"] = new JsonObject
            {
                ["@type"] = "Organization",
                ["name"] = "GoatLab",
                ["url"] = origin + "/",
            },
        };

        return root.ToJsonString();
    }
}
