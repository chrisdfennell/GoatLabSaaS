using System.Text;
using System.Xml;
using GoatLab.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Dynamic sitemap.xml for search-engine discovery. Lists static landing pages
// plus every public resource (breed directory, breed detail pages, public farm
// pages) so Googlebot/Bingbot don't have to infer them. Cached for 6 hours.
//
// IMPORTANT: the static /sitemap.xml previously served from wwwroot has been
// removed so this controller is the single source of truth.
[ApiController]
[AllowAnonymous]
public class SitemapController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public SitemapController(GoatLabDbContext db) => _db = db;

    [HttpGet("/sitemap.xml")]
    [ResponseCache(Duration = 21600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var origin = $"{Request.Scheme}://{Request.Host.Value}";

        var urls = new List<SitemapEntry>
        {
            new($"{origin}/",         "daily",   1.0),
            new($"{origin}/register", "monthly", 0.9),
            new($"{origin}/login",    "monthly", 0.5),
            new($"{origin}/terms",    "yearly",  0.3),
            new($"{origin}/privacy",  "yearly",  0.3),
            new($"{origin}/changelog","weekly",  0.5),
            new($"{origin}/breeds",   "daily",   0.8),
        };

        // Every breed slug currently represented by an active public listing.
        // Same filter as PublicController.ListBreeds so the sitemap only
        // advertises pages that actually have content.
        var breedPairs = await _db.Goats.IgnoreQueryFilters()
            .Where(g => g.IsListedForSale && !g.IsExternal
                        && g.Tenant!.PublicProfileEnabled
                        && g.Tenant.DeletedAt == null
                        && g.Tenant.SuspendedAt == null
                        && g.Breed != null && g.Breed != "")
            .Select(g => g.Breed)
            .ToListAsync(ct);

        var breedSlugs = breedPairs
            .Select(PublicController.BreedSlug)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();

        foreach (var slug in breedSlugs)
            urls.Add(new($"{origin}/breeds/{slug}", "weekly", 0.7));

        // Every public farm page.
        var farmSlugs = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.PublicProfileEnabled
                        && t.DeletedAt == null
                        && t.SuspendedAt == null)
            .Select(t => t.Slug)
            .ToListAsync(ct);

        foreach (var slug in farmSlugs)
            urls.Add(new($"{origin}/pub/{slug}", "weekly", 0.6));

        // Every for-sale goat — same gates as the SSR PublicFarmPagesController.
        // Listing changes are the events most worth crawling promptly, so a
        // weekly hint is fine; bots come back if traffic warrants.
        var goatSlugs = await _db.Goats.IgnoreQueryFilters()
            .Where(g => g.IsListedForSale && !g.IsExternal
                        && g.Tenant!.PublicProfileEnabled
                        && g.Tenant.DeletedAt == null
                        && g.Tenant.SuspendedAt == null)
            .Select(g => new { TenantSlug = g.Tenant!.Slug, g.Id })
            .ToListAsync(ct);

        foreach (var item in goatSlugs)
            urls.Add(new($"{origin}/pub/{item.TenantSlug}/{item.Id}", "weekly", 0.7));

        return Content(BuildXml(urls), "application/xml", Encoding.UTF8);
    }

    private record SitemapEntry(string Loc, string ChangeFreq, double Priority);

    private static string BuildXml(IReadOnlyList<SitemapEntry> entries)
    {
        var sb = new StringBuilder(4 * 1024);
        var settings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = false,
            Encoding = Encoding.UTF8,
        };
        using var writer = XmlWriter.Create(sb, settings);
        writer.WriteStartDocument();
        writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

        foreach (var entry in entries)
        {
            writer.WriteStartElement("url");
            writer.WriteElementString("loc", entry.Loc);
            writer.WriteElementString("changefreq", entry.ChangeFreq);
            writer.WriteElementString("priority", entry.Priority.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();
        return sb.ToString();
    }
}
