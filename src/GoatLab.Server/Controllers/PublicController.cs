using GoatLab.Server.Data;
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

    public PublicController(GoatLabDbContext db) => _db = db;

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
            BuildPedigreeNode(goat));

        Response.Headers.CacheControl = "public, max-age=300";
        return Ok(dto);
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
