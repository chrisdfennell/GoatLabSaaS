using GoatLab.Server.Data;
using GoatLab.Server.Services.Billing;
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
