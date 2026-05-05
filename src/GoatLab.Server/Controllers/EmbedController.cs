using GoatLab.Server.Data;
using GoatLab.Server.Services;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Embeddable widgets a farm can paste into their own website to drive traffic
// back to GoatLab — network expansion at zero cost. Two variants:
//   * /embed/badge/{slug}.svg     — static "Available on GoatLab" SVG sized
//                                   for a 200×60 button. Caches an hour.
//   * /embed/listings/{slug}      — full-page HTML (intended for <iframe>)
//                                   showing up to 6 current listings, light
//                                   styling that doesn't clash with most sites.
[AllowAnonymous]
[RequiresSaas]
public class EmbedController : Controller
{
    private readonly GoatLabDbContext _db;
    public EmbedController(GoatLabDbContext db) => _db = db;

    [HttpGet("/embed/badge/{slug}.svg")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Badge(string slug, CancellationToken ct)
    {
        var tenant = await GetPublicTenantAsync(slug, ct);
        if (tenant is null) return NotFound();

        var count = await _db.Goats.IgnoreQueryFilters()
            .CountAsync(g => g.TenantId == tenant.Id && g.IsListedForSale && !g.IsExternal, ct);

        var label = count switch
        {
            0 => "Available on GoatLab",
            1 => "1 goat for sale on GoatLab",
            _ => $"{count} goats for sale on GoatLab"
        };

        // Static dimensions intentionally; embedded site CSS shouldn't fight
        // an unspecified size.
        var svg = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 220 56"" width=""220"" height=""56"">
  <a xmlns:xlink=""http://www.w3.org/1999/xlink"" xlink:href=""{Origin()}/pub/{slug}"" target=""_blank"">
    <rect x=""0"" y=""0"" width=""220"" height=""56"" rx=""10"" fill=""#1b5e20""/>
    <rect x=""0"" y=""0"" width=""6"" height=""56"" fill=""#a5d6a7""/>
    <text x=""20"" y=""24"" font-family=""-apple-system, Segoe UI, Roboto, Helvetica, sans-serif""
          font-size=""11"" font-weight=""700"" fill=""#a5d6a7"" letter-spacing=""1"">🐐 GOATLAB</text>
    <text x=""20"" y=""44"" font-family=""-apple-system, Segoe UI, Roboto, Helvetica, sans-serif""
          font-size=""13"" font-weight=""600"" fill=""#fff"">{System.Net.WebUtility.HtmlEncode(label)}</text>
  </a>
</svg>";
        return Content(svg, "image/svg+xml; charset=utf-8");
    }

    [HttpGet("/embed/listings/{slug}")]
    [ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Listings(string slug, CancellationToken ct)
    {
        var tenant = await GetPublicTenantAsync(slug, ct);
        if (tenant is null) return NotFound();

        var goats = await _db.Goats.IgnoreQueryFilters()
            .Where(g => g.TenantId == tenant.Id && g.IsListedForSale && !g.IsExternal)
            .OrderBy(g => g.Name)
            .Take(6)
            .Select(g => new
            {
                g.Id, g.Name, g.Breed, g.Gender, g.AskingPriceCents,
                Photo = g.Photos.OrderByDescending(p => p.IsPrimary)
                    .ThenBy(p => p.UploadedAt)
                    .Select(p => p.FilePath).FirstOrDefault()
            })
            .ToListAsync(ct);

        // X-Frame-Options ALLOWALL is intentional — this endpoint exists to be
        // iframed onto third-party breeder websites. The page contains no
        // sensitive data; it's the same listings already public at /pub/{slug}.
        Response.Headers.Remove("X-Frame-Options");
        Response.Headers["Content-Security-Policy"] = "frame-ancestors *;";

        var origin = Origin();
        var sb = new System.Text.StringBuilder(4096);
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"/><title>");
        sb.Append(System.Net.WebUtility.HtmlEncode(tenant.Name));
        sb.Append(" — for sale on GoatLab</title>");
        sb.Append("<style>")
          .Append("*{box-sizing:border-box}")
          .Append("body{margin:0;font-family:-apple-system,Segoe UI,Roboto,sans-serif;background:transparent;color:#1a2421;padding:8px}")
          .Append(".g{display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:12px}")
          .Append(".c{background:#fff;border:1px solid #e0e0e0;border-radius:8px;text-decoration:none;color:inherit;overflow:hidden;display:block;transition:transform .15s,box-shadow .15s}")
          .Append(".c:hover{transform:translateY(-2px);box-shadow:0 4px 14px rgba(0,0,0,.08)}")
          .Append(".p{aspect-ratio:4/3;background:#f3f5f3;display:flex;align-items:center;justify-content:center;font-size:2rem;color:#bbb;overflow:hidden}")
          .Append(".p img{width:100%;height:100%;object-fit:cover;display:block}")
          .Append(".b{padding:8px 10px;font-size:13px}")
          .Append(".n{font-weight:700;color:#1a2421}")
          .Append(".m{color:#6b7a70;font-size:12px;margin-top:2px}")
          .Append(".pr{color:#2e7d32;font-weight:700;margin-top:4px}")
          .Append(".f{margin-top:10px;text-align:center;font-size:11px;color:#6b7a70}")
          .Append(".f a{color:#2e7d32;font-weight:600;text-decoration:none}")
          .Append("</style></head><body>");

        if (goats.Count == 0)
        {
            sb.Append("<div style=\"padding:24px;text-align:center;color:#6b7a70;font-size:13px;\">No goats currently listed.</div>");
        }
        else
        {
            sb.Append("<div class=\"g\">");
            foreach (var g in goats)
            {
                sb.Append("<a class=\"c\" target=\"_blank\" href=\"").Append(origin).Append("/pub/").Append(slug).Append("/").Append(g.Id).Append("\">");
                if (string.IsNullOrEmpty(g.Photo))
                    sb.Append("<div class=\"p\">🐐</div>");
                else
                    sb.Append("<div class=\"p\"><img loading=\"lazy\" src=\"").Append(origin).Append("/").Append(g.Photo).Append("\" alt=\"\"/></div>");
                sb.Append("<div class=\"b\"><div class=\"n\">").Append(System.Net.WebUtility.HtmlEncode(g.Name)).Append("</div>");
                sb.Append("<div class=\"m\">").Append(System.Net.WebUtility.HtmlEncode(g.Breed ?? "")).Append(" · ").Append(g.Gender).Append("</div>");
                if (g.AskingPriceCents.HasValue)
                    sb.Append("<div class=\"pr\">$").Append((g.AskingPriceCents.Value / 100m).ToString("N0")).Append("</div>");
                sb.Append("</div></a>");
            }
            sb.Append("</div>");
        }

        sb.Append("<div class=\"f\">Powered by <a href=\"").Append(origin).Append("/pub/").Append(slug).Append("\" target=\"_blank\">GoatLab</a></div>");
        sb.Append("</body></html>");

        return Content(sb.ToString(), "text/html; charset=utf-8");
    }

    private async Task<Tenant?> GetPublicTenantAsync(string slug, CancellationToken ct) =>
        await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == slug
                                      && t.PublicProfileEnabled
                                      && t.DeletedAt == null
                                      && t.SuspendedAt == null, ct);

    private string Origin() => $"{Request.Scheme}://{Request.Host}";
}
