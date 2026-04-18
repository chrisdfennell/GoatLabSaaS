using GoatLab.Server.Data;
using GoatLab.Server.Services.ApiKeys;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Anonymous read-only view of a single waitlist reservation. Auth is the
// token in the URL — no tenant_id claim is set, so every query uses
// IgnoreQueryFilters and derives tenant scope from the token's own row.
//
// Reservations are linked to a customer, and the customer's name, the farm
// name, the reservation status, and (when Offered/Fulfilled) the reserved
// goat's public-safe fields are returned. No owner PII, no finance data.
[ApiController]
[AllowAnonymous]
[Route("api/buyer")]
public class BuyerPortalController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public BuyerPortalController(GoatLabDbContext db) => _db = db;

    public record BuyerPortalView(
        string FarmName,
        string CustomerName,
        string ReservationStatus,
        DateTime ReservationCreatedAt,
        DateTime? OfferedAt,
        DateTime? FulfilledAt,
        string? BreedPreference,
        string? SexPreference,
        string? ColorPreference,
        int DepositCents,
        bool DepositPaid,
        DateTime? DepositReceivedAt,
        BuyerGoatView? Goat);

    public record BuyerGoatView(
        int Id,
        string Name,
        string? Breed,
        string Gender,
        DateTime? DateOfBirth,
        string? Bio,
        string? PrimaryPhotoUrl,
        IReadOnlyList<string> PhotoUrls,
        double? LatestWeightLbs,
        DateTime? LatestWeightDate);

    [HttpGet("{token}")]
    public async Task<ActionResult<BuyerPortalView>> Get(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return NotFound();
        var hash = ApiKeyGenerator.HashPlaintext(token);

        var now = DateTime.UtcNow;
        var access = await _db.BuyerAccessTokens.IgnoreQueryFilters()
            .Include(t => t.Tenant)
            .Include(t => t.WaitlistEntry).ThenInclude(w => w!.Customer)
            .Include(t => t.WaitlistEntry).ThenInclude(w => w!.FulfilledGoat)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (access is null) return NotFound();
        if (access.RevokedAt is not null) return NotFound(new { error = "Link revoked." });
        if (access.ExpiresAt <= now) return NotFound(new { error = "Link expired." });

        var entry = access.WaitlistEntry;
        if (entry is null || entry.Customer is null) return NotFound();
        if (entry.Tenant?.DeletedAt is not null) return NotFound();

        // Stamp LastUsedAt fire-and-forget style (still synchronous — this is a
        // single short-running request).
        access.LastUsedAt = now;
        await _db.SaveChangesAsync(ct);

        // Reveal the goat once the reservation reaches Offered. Before that,
        // there's nothing concrete to show yet.
        BuyerGoatView? goat = null;
        int? goatId = entry.FulfilledGoatId;
        if (goatId is null && entry.Status == WaitlistStatus.Offered)
        {
            // Offered-without-a-linked-goat is possible if the owner manually
            // flipped status without picking a goat. Just leave goat null.
        }
        if (goatId is int gid)
        {
            var g = await _db.Goats.IgnoreQueryFilters()
                .Include(x => x.Photos)
                .FirstOrDefaultAsync(x => x.Id == gid && x.TenantId == access.TenantId, ct);
            if (g is not null)
            {
                var orderedPhotos = g.Photos
                    .OrderByDescending(p => p.IsPrimary).ThenBy(p => p.UploadedAt)
                    .Select(p => "/" + p.FilePath).ToList();
                var latestWeight = await _db.WeightRecords.IgnoreQueryFilters()
                    .Where(w => w.GoatId == gid && w.TenantId == access.TenantId)
                    .OrderByDescending(w => w.Date)
                    .Select(w => new { w.Weight, w.Date })
                    .FirstOrDefaultAsync(ct);
                goat = new BuyerGoatView(
                    g.Id, g.Name, g.Breed, g.Gender.ToString(),
                    g.DateOfBirth, g.Bio,
                    orderedPhotos.FirstOrDefault(),
                    orderedPhotos,
                    latestWeight?.Weight,
                    latestWeight?.Date);
            }
        }

        Response.Headers.CacheControl = "private, no-store";
        return Ok(new BuyerPortalView(
            access.Tenant?.Name ?? "Farm",
            entry.Customer.Name,
            entry.Status.ToString(),
            entry.CreatedAt,
            entry.OfferedAt,
            entry.FulfilledAt,
            entry.BreedPreference,
            entry.SexPreference?.ToString(),
            entry.ColorPreference,
            entry.DepositCents,
            entry.DepositPaid,
            entry.DepositReceivedAt,
            goat));
    }
}
