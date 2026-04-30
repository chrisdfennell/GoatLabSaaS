using System.Text;
using System.Xml;
using GoatLab.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Public RSS 2.0 feeds for the marketplace. Breeder Discords / forums / RSS
// readers (NetNewsWire, Feedly, etc.) auto-aggregate these — free distribution
// every time a farm lists a new goat. Two surfaces:
//   * /feed/listings.xml          — every active for-sale listing
//   * /feed/breeds/{slug}.xml     — listings filtered to a specific breed
//
// Cached for 10 minutes to keep this safe under aggressive RSS polling. Items
// are sorted by Goat.UpdatedAt descending — close enough to "newest first"
// without adding a separate ListedAt column. Cap at 50 items per feed.
[AllowAnonymous]
public class FeedsController : Controller
{
    private readonly GoatLabDbContext _db;
    public FeedsController(GoatLabDbContext db) => _db = db;

    [HttpGet("/feed/listings.xml")]
    [ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Listings(CancellationToken ct)
        => Content(await BuildFeedAsync(breedSlug: null, ct), "application/rss+xml; charset=utf-8");

    [HttpGet("/feed/breeds/{slug}.xml")]
    [ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> ByBreed(string slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug)) return NotFound();
        return Content(await BuildFeedAsync(breedSlug: slug, ct), "application/rss+xml; charset=utf-8");
    }

    private async Task<string> BuildFeedAsync(string? breedSlug, CancellationToken ct)
    {
        var origin = $"{Request.Scheme}://{Request.Host}";
        var rows = await _db.Goats.IgnoreQueryFilters()
            .Where(g => g.IsListedForSale && !g.IsExternal
                        && g.Tenant!.PublicProfileEnabled
                        && g.Tenant.DeletedAt == null
                        && g.Tenant.SuspendedAt == null
                        && g.Breed != null && g.Breed != "")
            .OrderByDescending(g => g.UpdatedAt)
            .Take(200) // pre-filter cap; we drop to 50 after slug match
            .Select(g => new
            {
                g.Id, g.Name, g.Breed, g.Gender, g.AskingPriceCents, g.UpdatedAt,
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

        // BreedSlug match must happen client-side (static C# method, not
        // SQL-translatable). Keep the projection small so this is cheap.
        if (!string.IsNullOrWhiteSpace(breedSlug))
            rows = rows.Where(r => PublicController.BreedSlug(r.Breed) == breedSlug).ToList();

        rows = rows.Take(50).ToList();

        var feedTitle = breedSlug is null
            ? "GoatLab — All for-sale listings"
            : $"GoatLab — {breedSlug} listings";
        var feedDesc = breedSlug is null
            ? "New goats listed for sale across every public GoatLab farm."
            : $"New {breedSlug} goats listed for sale across every public GoatLab farm.";
        var feedLink = breedSlug is null
            ? $"{origin}/breeds"
            : $"{origin}/breeds/{breedSlug}";
        var selfHref = breedSlug is null
            ? $"{origin}/feed/listings.xml"
            : $"{origin}/feed/breeds/{breedSlug}.xml";

        var sb = new StringBuilder(8 * 1024);
        var xws = new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true, OmitXmlDeclaration = false };
        using var writer = XmlWriter.Create(sb, xws);
        writer.WriteStartDocument();
        writer.WriteStartElement("rss");
        writer.WriteAttributeString("version", "2.0");
        writer.WriteAttributeString("xmlns", "atom", null, "http://www.w3.org/2005/Atom");

        writer.WriteStartElement("channel");
        writer.WriteElementString("title", feedTitle);
        writer.WriteElementString("link", feedLink);
        writer.WriteElementString("description", feedDesc);
        writer.WriteElementString("language", "en-us");
        writer.WriteElementString("generator", "GoatLab");
        writer.WriteElementString("ttl", "10");
        // atom:link self-reference is required by RSS validators.
        writer.WriteStartElement("link", "http://www.w3.org/2005/Atom");
        writer.WriteAttributeString("href", selfHref);
        writer.WriteAttributeString("rel", "self");
        writer.WriteAttributeString("type", "application/rss+xml");
        writer.WriteEndElement();

        if (rows.Count > 0)
        {
            writer.WriteElementString("lastBuildDate",
                rows[0].UpdatedAt.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }

        foreach (var r in rows)
        {
            var url = $"{origin}/pub/{r.TenantSlug}/{r.Id}";
            var price = r.AskingPriceCents.HasValue ? $" — ${(r.AskingPriceCents.Value / 100m):N0}" : "";
            var loc = string.IsNullOrEmpty(r.TenantLocation) ? "" : $" ({r.TenantLocation})";
            var title = $"{r.Name} · {r.Breed} · {r.Gender} at {r.TenantName}{loc}{price}";
            var photoHtml = string.IsNullOrEmpty(r.PrimaryPhoto)
                ? ""
                : $"<p><img src=\"{origin}/{r.PrimaryPhoto}\" alt=\"{System.Net.WebUtility.HtmlEncode(r.Name)}\" /></p>";
            var descHtml = $"{photoHtml}<p>{r.Breed} {r.Gender}{price} — listed by <a href=\"{origin}/pub/{r.TenantSlug}\">{System.Net.WebUtility.HtmlEncode(r.TenantName)}</a>{System.Net.WebUtility.HtmlEncode(loc)}.</p>";

            writer.WriteStartElement("item");
            writer.WriteElementString("title", title);
            writer.WriteElementString("link", url);
            writer.WriteStartElement("guid");
            writer.WriteAttributeString("isPermaLink", "true");
            writer.WriteString(url);
            writer.WriteEndElement();
            writer.WriteElementString("pubDate",
                r.UpdatedAt.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteElementString("description", descHtml);
            writer.WriteEndElement(); // item
        }

        writer.WriteEndElement(); // channel
        writer.WriteEndElement(); // rss
        writer.WriteEndDocument();
        writer.Flush();
        return sb.ToString();
    }
}
