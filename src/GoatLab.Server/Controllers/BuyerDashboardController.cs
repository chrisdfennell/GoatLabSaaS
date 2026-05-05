using System.Security.Claims;
using GoatLab.Server.Data;
using GoatLab.Server.Services;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Buyer-side dashboard + saved-listing CRUD. All endpoints (UI + JSON)
// require the "Buyer" cookie scheme; anonymous browsers get redirected to
// /buyer/sign-in or 401'd by the cookie middleware in Program.cs.
[Authorize(AuthenticationSchemes = "Buyer")]
[RequiresSaas]
public class BuyerDashboardController : Controller
{
    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;

    public BuyerDashboardController(GoatLabDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    // ----- Dashboard SSR view -----

    [HttpGet("/buyer/dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        if (!TryGetBuyer(out var buyerId, out var email)) return Challenge();

        // No tenant on the buyer cookie — bypass the global filter so we can
        // load cross-tenant marketplace data (saved listings live across many
        // farms; inquiries the buyer sent to many sellers).
        _tenantContext.BypassFilter = true;

        var saved = await _db.BuyerSavedListings.IgnoreQueryFilters()
            .Where(s => s.BuyerAccountId == buyerId)
            .OrderByDescending(s => s.SavedAt)
            .Select(s => new BuyerDashboardVm.SavedRow(
                s.Goat!.Id,
                s.Goat.Name,
                s.Goat.Breed,
                s.Goat.Gender.ToString(),
                s.Goat.AskingPriceCents,
                s.Goat.Tenant!.Slug,
                s.Goat.Tenant.Name,
                s.Goat.Tenant.Location,
                s.Goat.Photos
                    .OrderByDescending(p => p.IsPrimary)
                    .ThenBy(p => p.UploadedAt)
                    .Select(p => "/" + p.FilePath)
                    .FirstOrDefault(),
                s.Goat.IsListedForSale,
                s.SavedAt))
            .ToListAsync(ct);

        var inquiries = await _db.BuyerInquiries.IgnoreQueryFilters()
            .Where(i => i.BuyerEmail == email)
            .OrderByDescending(i => i.LastMessageAt)
            .Take(50)
            .Select(i => new BuyerDashboardVm.InquiryRow(
                i.Id,
                i.Goat!.Name,
                i.Tenant!.Slug,
                i.Tenant.Name,
                i.Status.ToString(),
                i.LastMessageAt,
                i.Messages.Count))
            .ToListAsync(ct);

        var follows = await _db.FarmFollowers.IgnoreQueryFilters()
            .Where(f => f.Email == email && f.IsActive)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new BuyerDashboardVm.FollowRow(
                f.Tenant!.Slug,
                f.Tenant.Name,
                f.Tenant.Location,
                f.CreatedAt))
            .ToListAsync(ct);

        var alerts = await _db.MarketplaceAlerts.IgnoreQueryFilters()
            .Where(a => a.Email == email && a.IsActive)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new BuyerDashboardVm.AlertRow(
                a.BreedSlug, a.Sex, a.MinPriceCents, a.MaxPriceCents, a.StateMatch, a.CreatedAt))
            .ToListAsync(ct);

        return View(new BuyerDashboardVm(email, saved, inquiries, follows, alerts));
    }

    // ----- Saved-listing CRUD -----

    [HttpGet("/api/buyer/saved")]
    public async Task<ActionResult<List<int>>> GetSavedIds(CancellationToken ct)
    {
        if (!TryGetBuyer(out var buyerId, out _)) return Unauthorized();
        return await _db.BuyerSavedListings
            .Where(s => s.BuyerAccountId == buyerId)
            .Select(s => s.GoatId)
            .ToListAsync(ct);
    }

    public record SaveRequest(int GoatId);

    [HttpPost("/api/buyer/saved")]
    public async Task<IActionResult> Save([FromBody] SaveRequest req, CancellationToken ct)
    {
        if (!TryGetBuyer(out var buyerId, out _)) return Unauthorized();

        // Verify the goat exists + is publicly listed before saving so we don't
        // collect refs to private/deleted rows. Bypass the filter — the buyer
        // has no tenant context.
        _tenantContext.BypassFilter = true;
        var exists = await _db.Goats.IgnoreQueryFilters()
            .AnyAsync(g => g.Id == req.GoatId
                        && g.IsListedForSale
                        && g.Tenant!.PublicProfileEnabled
                        && g.Tenant.DeletedAt == null
                        && g.Tenant.SuspendedAt == null, ct);
        if (!exists) return NotFound();

        var existing = await _db.BuyerSavedListings
            .FirstOrDefaultAsync(s => s.BuyerAccountId == buyerId && s.GoatId == req.GoatId, ct);
        if (existing is not null) return NoContent(); // idempotent

        _db.BuyerSavedListings.Add(new BuyerSavedListing
        {
            BuyerAccountId = buyerId,
            GoatId = req.GoatId,
            SavedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("/api/buyer/saved/{goatId:int}")]
    public async Task<IActionResult> Unsave(int goatId, CancellationToken ct)
    {
        if (!TryGetBuyer(out var buyerId, out _)) return Unauthorized();

        var row = await _db.BuyerSavedListings
            .FirstOrDefaultAsync(s => s.BuyerAccountId == buyerId && s.GoatId == goatId, ct);
        if (row is null) return NoContent();

        _db.BuyerSavedListings.Remove(row);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("/api/buyer/me")]
    public IActionResult Me()
    {
        if (!TryGetBuyer(out _, out var email)) return Unauthorized();
        return Ok(new { email });
    }

    private bool TryGetBuyer(out int id, out string email)
    {
        id = 0;
        email = string.Empty;
        var idStr = User.FindFirstValue("buyer_id");
        var em = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(idStr) || string.IsNullOrEmpty(em)) return false;
        if (!int.TryParse(idStr, out id)) return false;
        email = em;
        return true;
    }
}

public record BuyerDashboardVm(
    string Email,
    IReadOnlyList<BuyerDashboardVm.SavedRow> Saved,
    IReadOnlyList<BuyerDashboardVm.InquiryRow> Inquiries,
    IReadOnlyList<BuyerDashboardVm.FollowRow> Follows,
    IReadOnlyList<BuyerDashboardVm.AlertRow> Alerts)
{
    public record SavedRow(
        int GoatId, string Name, string? Breed, string Gender, int? AskingPriceCents,
        string FarmSlug, string FarmName, string? FarmLocation, string? PrimaryPhotoUrl,
        bool StillListed, DateTime SavedAt);

    public record InquiryRow(
        int Id, string GoatName, string FarmSlug, string FarmName,
        string Status, DateTime LastMessageAt, int MessageCount);

    public record FollowRow(string FarmSlug, string FarmName, string? FarmLocation, DateTime FollowedAt);

    public record AlertRow(
        string? BreedSlug, string? Sex, int? MinPriceCents, int? MaxPriceCents,
        string? State, DateTime CreatedAt);
}
