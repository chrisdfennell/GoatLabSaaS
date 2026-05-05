using System.Security.Cryptography;
using GoatLab.Server.Data;
using GoatLab.Server.Filters;
using GoatLab.Server.Services;
using GoatLab.Server.Services.Billing;
using GoatLab.Server.Services.Email;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Anon-readable goat-for-sale pages. Two gates per request:
//   1. Tenant.PublicProfileEnabled must be true (owner opt-in).
//   2. Goat.IsListedForSale must be true.
// Always queries with IgnoreQueryFilters() because there is no tenant_id claim
// on these requests; tenant scope is enforced by the slug-match in the WHERE.
[ApiController]
[AllowAnonymous]
[Route("api/public")]
[RequiresSaas]
public class PublicController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly IBillingService _billing;

    public PublicController(GoatLabDbContext db, IBillingService billing)
    {
        _db = db;
        _billing = billing;
    }

    [HttpGet("farms/{slug}/goats")]
    public async Task<ActionResult<IReadOnlyList<PublicGoatListItemDto>>> List(string slug, CancellationToken ct)
    {
        var tenant = await GetPublicTenantAsync(slug, ct);
        if (tenant is null) return NotFound();

        var goats = await _db.Goats.IgnoreQueryFilters()
            .Where(g => g.TenantId == tenant.Id && g.IsListedForSale && !g.IsExternal)
            .OrderBy(g => g.Name)
            .Select(g => new PublicGoatListItemDto(
                g.Id, g.Name, g.EarTag, g.Breed, g.Gender.ToString(),
                g.DateOfBirth, g.AskingPriceCents,
                g.Photos
                    .OrderByDescending(p => p.IsPrimary)
                    .ThenBy(p => p.UploadedAt)
                    .Select(p => "/" + p.FilePath)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        Response.Headers.CacheControl = "public, max-age=300";
        return Ok(goats);
    }

    [HttpGet("farms/{slug}/goats/{goatId:int}")]
    public async Task<ActionResult<PublicGoatDto>> Get(string slug, int goatId, CancellationToken ct)
    {
        var tenant = await GetPublicTenantAsync(slug, ct);
        if (tenant is null) return NotFound();

        var goat = await _db.Goats.IgnoreQueryFilters()
            .Include(g => g.Photos)
            .Include(g => g.Sire).ThenInclude(s => s!.Sire)
            .Include(g => g.Sire).ThenInclude(s => s!.Dam)
            .Include(g => g.Dam).ThenInclude(d => d!.Sire)
            .Include(g => g.Dam).ThenInclude(d => d!.Dam)
            .FirstOrDefaultAsync(g => g.Id == goatId && g.TenantId == tenant.Id && g.IsListedForSale, ct);
        if (goat is null) return NotFound();

        var depositCents = ComputeDepositCents(tenant, goat);

        var dto = new PublicGoatDto(
            goat.Id,
            goat.Name,
            goat.EarTag,
            goat.Breed,
            goat.Gender.ToString(),
            goat.DateOfBirth,
            goat.Bio,
            goat.Registry == GoatRegistry.None ? null : goat.Registry.ToString(),
            goat.RegistrationNumber,
            goat.BreederName,
            goat.AskingPriceCents,
            goat.SaleNotes,
            tenant.Name,
            tenant.Slug,
            tenant.Location,
            tenant.PublicContactEmail,
            goat.Photos
                .OrderByDescending(p => p.IsPrimary)
                .ThenBy(p => p.UploadedAt)
                .Select(p => new PublicGoatPhotoDto("/" + p.FilePath, p.Caption, p.IsPrimary))
                .ToList(),
            BuildPedigreeNode(goat),
            depositCents);

        Response.Headers.CacheControl = "public, max-age=300";
        return Ok(dto);
    }

    [HttpPost("farms/{slug}/goats/{goatId:int}/reserve")]
    [RequireRecaptcha("reserve")]
    public async Task<ActionResult<RedirectUrlDto>> Reserve(
        string slug,
        int goatId,
        [FromBody] PublicReservationRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.BuyerEmail) || string.IsNullOrWhiteSpace(req.BuyerName))
            return BadRequest(new { error = "Email and name are required." });

        var tenant = await GetPublicTenantAsync(slug, ct);
        if (tenant is null) return NotFound();

        var goat = await _db.Goats.IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Id == goatId && g.TenantId == tenant.Id && g.IsListedForSale, ct);
        if (goat is null) return NotFound();

        var depositCents = ComputeDepositCents(tenant, goat);
        if (depositCents is null or <= 0)
            return BadRequest(new { error = "This farm is not accepting online deposits for this listing." });

        var origin = RequestOrigin();
        var url = await _billing.CreateDepositCheckoutSessionAsync(
            tenant,
            goat,
            depositCents.Value,
            req,
            origin,
            ct);

        return new RedirectUrlDto(url);
    }

    public record RedirectUrlDto(string Url);

    private string RequestOrigin()
    {
        var origin = Request.Headers["Origin"].ToString();
        if (!string.IsNullOrEmpty(origin)) return origin;
        return $"{Request.Scheme}://{Request.Host}";
    }

    // ----- Anonymous "Follow this farm" -----

    public record FollowFarmRequest(string Email);
    public record FollowFarmResponse(bool Ok, string Message);

    [HttpPost("farms/{slug}/follow")]
    [RequireRecaptcha("follow_farm")]
    public async Task<ActionResult<FollowFarmResponse>> Follow(
        string slug,
        [FromBody] FollowFarmRequest req,
        [FromServices] IAppEmailSender email,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
            return new FollowFarmResponse(false, "Please enter a valid email address.");

        var tenant = await GetPublicTenantAsync(slug, ct);
        if (tenant is null) return NotFound(new FollowFarmResponse(false, "Farm not found or not public."));

        var emailNorm = req.Email.Trim().ToLowerInvariant();

        // Idempotent: if a row already exists for this (tenant, email) we
        // reactivate (in case of prior unsubscribe) and resend the
        // confirmation. No "already following" leak — same response either way.
        var existing = await _db.FarmFollowers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.TenantId == tenant.Id && f.Email == emailNorm, ct);

        FarmFollower follower;
        if (existing is not null)
        {
            existing.IsActive = true;
            existing.UnsubscribedAt = null;
            // Rotate token on resubscribe so a stale unsubscribe link from
            // an earlier session can't quietly re-deactivate.
            existing.UnsubscribeToken = NewToken();
            follower = existing;
        }
        else
        {
            follower = new FarmFollower
            {
                TenantId = tenant.Id,
                Email = emailNorm,
                CreatedAt = DateTime.UtcNow,
                UnsubscribeToken = NewToken(),
                IsActive = true,
            };
            _db.FarmFollowers.Add(follower);
        }
        await _db.SaveChangesAsync(ct);

        var origin = RequestOrigin();
        var farmUrl = $"{origin}/pub/{tenant.Slug}";
        var unsubUrl = $"{origin}/follow/unsubscribe?token={follower.UnsubscribeToken}";

        try
        {
            var (subject, html, text) = EmailTemplates.FarmFollowConfirmation(tenant.Name, farmUrl, unsubUrl);
            await email.SendAsync(emailNorm, subject, html, text, ct);
        }
        catch
        {
            // Email failure shouldn't block the subscribe — they're already
            // in the DB and the next listing will reach them. Logged by the
            // LoggingEmailSenderDecorator.
        }

        return new FollowFarmResponse(true, $"You're now following {tenant.Name}. Check your email to confirm.");
    }

    // ----- Saved-search marketplace alerts -----

    public record AlertRequest(
        string Email,
        string? BreedSlug,        // optional; matches `/breeds/{slug}` URLs
        string? Sex,              // "Male" | "Female" | "Wether" | null
        int? MinPriceCents,
        int? MaxPriceCents,
        string? State);

    public record AlertResponse(bool Ok, string Message);

    [HttpPost("/api/marketplace/alerts")]
    [RequireRecaptcha("marketplace_alert")]
    public async Task<ActionResult<AlertResponse>> CreateAlert(
        [FromBody] AlertRequest req,
        [FromServices] IAppEmailSender email,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
            return new AlertResponse(false, "Please enter a valid email address.");

        var emailNorm = req.Email.Trim().ToLowerInvariant();
        // Normalize the breed slug if the buyer typed a free-form breed name —
        // BreedSlug is what NewListingNotifier matches against.
        var slugNorm = string.IsNullOrWhiteSpace(req.BreedSlug)
            ? null
            : BreedSlug(req.BreedSlug.Trim());

        // Validate sex against the enum names so a typo doesn't ghost-create a
        // never-matching alert. Null = any sex.
        string? sexNorm = null;
        if (!string.IsNullOrWhiteSpace(req.Sex))
        {
            var s = req.Sex.Trim();
            if (string.Equals(s, "Male", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Female", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Wether", StringComparison.OrdinalIgnoreCase))
                sexNorm = char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
            else
                return new AlertResponse(false, "Sex must be Male, Female, or Wether.");
        }

        // Idempotent on (Email + criteria). If the same buyer registers the
        // same search twice, reactivate (in case of prior unsubscribe) and
        // rotate the token so a stale unsubscribe link can't kill the new one.
        var existing = await _db.MarketplaceAlerts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Email == emailNorm
                && a.BreedSlug == slugNorm && a.Sex == sexNorm
                && a.MinPriceCents == req.MinPriceCents && a.MaxPriceCents == req.MaxPriceCents
                && a.StateMatch == req.State, ct);

        MarketplaceAlert alert;
        if (existing is not null)
        {
            existing.IsActive = true;
            existing.UnsubscribedAt = null;
            existing.UnsubscribeToken = NewToken();
            alert = existing;
        }
        else
        {
            alert = new MarketplaceAlert
            {
                Email = emailNorm,
                BreedSlug = slugNorm,
                Sex = sexNorm,
                MinPriceCents = req.MinPriceCents,
                MaxPriceCents = req.MaxPriceCents,
                StateMatch = string.IsNullOrWhiteSpace(req.State) ? null : req.State.Trim(),
                UnsubscribeToken = NewToken(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            _db.MarketplaceAlerts.Add(alert);
        }
        await _db.SaveChangesAsync(ct);

        var origin = RequestOrigin();
        var label = string.Join(" · ", new[]
        {
            slugNorm ?? "any breed",
            sexNorm?.ToLowerInvariant(),
            req.MinPriceCents.HasValue && req.MaxPriceCents.HasValue
                ? $"${req.MinPriceCents / 100}-${req.MaxPriceCents / 100}"
                : req.MaxPriceCents.HasValue ? $"under ${req.MaxPriceCents / 100}"
                : req.MinPriceCents.HasValue ? $"≥ ${req.MinPriceCents / 100}" : null,
            string.IsNullOrWhiteSpace(req.State) ? null : $"in {req.State.Trim()}",
        }.Where(s => !string.IsNullOrEmpty(s)));

        var browseUrl = slugNorm is null ? $"{origin}/breeds" : $"{origin}/breeds/{slugNorm}";
        var unsubUrl = $"{origin}/alerts/unsubscribe?token={alert.UnsubscribeToken}";

        try
        {
            var (subject, html, text) = EmailTemplates.MarketplaceAlertConfirmation(label, browseUrl, unsubUrl);
            await email.SendAsync(emailNorm, subject, html, text, ct);
        }
        catch
        {
            // Email failure shouldn't block the subscribe. Logged by decorator.
        }

        return new AlertResponse(true, "Saved! Check your email to confirm.");
    }

    [HttpGet("/alerts/unsubscribe")]
    public async Task<ContentResult> UnsubscribeAlert([FromQuery] string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return UnsubscribePage(false, "Missing or invalid unsubscribe token.");

        var alert = await _db.MarketplaceAlerts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.UnsubscribeToken == token, ct);
        if (alert is null)
            return UnsubscribePage(false, "We couldn't find that saved search. It may already be removed.");

        if (alert.IsActive)
        {
            alert.IsActive = false;
            alert.UnsubscribedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return UnsubscribePage(true, "Your saved search has been removed. You'll no longer receive match notifications.");
    }

    // GET so it works from a one-click email link. Returns a tiny HTML page —
    // we don't care about the JSON consumer experience here, only the buyer
    // who clicked the unsubscribe button in their inbox.
    [HttpGet("/follow/unsubscribe")]
    public async Task<ContentResult> Unsubscribe([FromQuery] string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return UnsubscribePage(false, "Missing or invalid unsubscribe token.");

        var follower = await _db.FarmFollowers.IgnoreQueryFilters()
            .Include(f => f.Tenant)
            .FirstOrDefaultAsync(f => f.UnsubscribeToken == token, ct);

        if (follower is null)
            return UnsubscribePage(false, "We couldn't find that subscription. It may have already been removed.");

        if (follower.IsActive)
        {
            follower.IsActive = false;
            follower.UnsubscribedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        var farmName = follower.Tenant?.Name ?? "this farm";
        return UnsubscribePage(true, $"You've been unsubscribed from {farmName} new-listing notifications.");
    }

    private static string NewToken()
    {
        // 32 random bytes → 64 hex chars. Plenty of entropy for an
        // unsubscribe link; collision-resistant per the unique index.
        var buf = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(buf).ToLowerInvariant();
    }

    private ContentResult UnsubscribePage(bool success, string message)
    {
        var icon = success ? "✓" : "✗";
        var color = success ? "#2e7d32" : "#c62828";
        var html = $@"<!DOCTYPE html><html><head><meta charset=""utf-8"" /><title>Unsubscribe — GoatLab</title>
<meta name=""viewport"" content=""width=device-width,initial-scale=1"" /></head>
<body style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;background:#f3f5f3;margin:0;padding:48px 16px;text-align:center;color:#1a2421;"">
  <div style=""max-width:460px;margin:0 auto;background:#fff;padding:32px;border-radius:12px;box-shadow:0 1px 3px rgba(0,0,0,0.06);"">
    <div style=""font-size:48px;color:{color};line-height:1;"">{icon}</div>
    <h1 style=""font-size:20px;margin:16px 0 8px 0;"">Unsubscribed</h1>
    <p style=""color:#4a5a51;margin:0 0 24px 0;"">{System.Net.WebUtility.HtmlEncode(message)}</p>
    <a href=""/breeds"" style=""display:inline-block;background:#2e7d32;color:#fff;text-decoration:none;padding:12px 22px;border-radius:8px;font-weight:600;"">Browse other breeders</a>
  </div>
</body></html>";
        return new ContentResult { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = success ? 200 : 404 };
    }

    // ----- Breed directory -----
    //
    // Aggregates every for-sale goat across every opted-in tenant, grouped by a
    // normalized breed slug so "Nigerian Dwarf", "nigerian dwarf", and "Nigerian
    // Dwarf " all cluster to "nigerian-dwarf". SEO-friendly (slug is stable and
    // Google-indexable). 1-hour cache because listings don't change that often.

    [HttpGet("breeds")]
    public async Task<ActionResult<IReadOnlyList<BreedSummaryDto>>> ListBreeds(CancellationToken ct)
    {
        var raw = await _db.Goats.IgnoreQueryFilters()
            .Where(g => g.IsListedForSale && !g.IsExternal
                        && g.Tenant!.PublicProfileEnabled
                        && g.Tenant.DeletedAt == null
                        && g.Tenant.SuspendedAt == null
                        && g.Breed != null && g.Breed != "")
            .Select(g => new { g.Breed, g.TenantId })
            .ToListAsync(ct);

        var grouped = raw
            .GroupBy(r => BreedSlug(r.Breed))
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .Select(g => new BreedSummaryDto(
                g.Key!,
                // Use the most common casing as the display name (stable and human-readable).
                g.GroupBy(r => r.Breed!.Trim())
                    .OrderByDescending(x => x.Count())
                    .First().Key,
                g.Select(r => r.TenantId).Distinct().Count(),
                g.Count()))
            .OrderByDescending(b => b.ListingCount)
            .ThenBy(b => b.DisplayName)
            .ToList();

        Response.Headers.CacheControl = "public, max-age=3600";
        return Ok(grouped);
    }

    [HttpGet("breeds/{breedSlug}")]
    public async Task<ActionResult<IReadOnlyList<BreedFarmDto>>> ListBreedFarms(string breedSlug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(breedSlug)) return BadRequest();

        // Pull enough to group client-side — normalized Breed match must happen in-memory
        // because EF can't translate our slug normalizer to SQL.
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

        var matches = raw.Where(r => BreedSlug(r.Breed) == breedSlug).ToList();
        if (matches.Count == 0) return NotFound();

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

        Response.Headers.CacheControl = "public, max-age=3600";
        return Ok(farms);
    }

    // "Nigerian Dwarf " → "nigerian-dwarf". Non-alphanumeric collapses to single dashes.
    public static string BreedSlug(string? breed)
    {
        if (string.IsNullOrWhiteSpace(breed)) return string.Empty;
        var sb = new System.Text.StringBuilder(breed.Length);
        var lastWasDash = true;
        foreach (var c in breed.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                sb.Append('-');
                lastWasDash = true;
            }
        }
        return sb.ToString().Trim('-');
    }

    private static int? ComputeDepositCents(Tenant tenant, Goat goat)
    {
        if (tenant.PublicDepositPercent <= 0) return null;
        if (goat.AskingPriceCents is not > 0) return null;
        var pct = Math.Clamp(tenant.PublicDepositPercent, 1, 100);
        var cents = (int)Math.Round(goat.AskingPriceCents.Value * (pct / 100.0));
        // Stripe minimum is 50 cents USD. Below that we won't offer the flow.
        return cents < 50 ? null : cents;
    }

    private async Task<Tenant?> GetPublicTenantAsync(string slug, CancellationToken ct)
    {
        return await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == slug
                && t.PublicProfileEnabled
                && t.DeletedAt == null
                && t.SuspendedAt == null, ct);
    }

    private static PublicPedigreeNodeDto? BuildPedigreeNode(Goat? g) => g is null
        ? null
        : new PublicPedigreeNodeDto(
            g.Id, g.Name, g.RegistrationNumber, g.Breed,
            BuildPedigreeNode(g.Sire),
            BuildPedigreeNode(g.Dam));
}
