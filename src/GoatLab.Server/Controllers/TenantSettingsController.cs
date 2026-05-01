using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Tenant-owner self-service settings. Super-admin CRUD for tenants lives in
// AdminController (different access + surface area).
[ApiController]
[Route("api/tenant")]
public class TenantSettingsController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFeatureGate _featureGate;

    public TenantSettingsController(
        GoatLabDbContext db,
        ITenantContext tenantContext,
        UserManager<ApplicationUser> userManager,
        IFeatureGate featureGate)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userManager = userManager;
        _featureGate = featureGate;
    }

    public record TenantSettingsDto(
        int Id,
        string Name,
        string Slug,
        string? Location,
        TenantUnits Units,
        bool AlertEmailEnabled,
        bool PublicProfileEnabled,
        string? PublicContactEmail,
        int PublicDepositPercent,
        DateTime CreatedAt,
        double? PublicLatitude,
        double? PublicLongitude);

    public record UpdateSettingsInput(
        string Name,
        string? Location,
        TenantUnits Units,
        bool AlertEmailEnabled,
        bool PublicProfileEnabled,
        string? PublicContactEmail,
        int PublicDepositPercent,
        double? PublicLatitude,
        double? PublicLongitude);

    [HttpGet]
    public async Task<ActionResult<TenantSettingsDto>> Get(CancellationToken ct)
    {
        if (_tenantContext.TenantId is not int tenantId) return NotFound();
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return NotFound();
        return new TenantSettingsDto(tenant.Id, tenant.Name, tenant.Slug, tenant.Location, tenant.Units, tenant.AlertEmailEnabled, tenant.PublicProfileEnabled, tenant.PublicContactEmail, tenant.PublicDepositPercent, tenant.CreatedAt, tenant.PublicLatitude, tenant.PublicLongitude);
    }

    [HttpPut]
    public async Task<ActionResult<TenantSettingsDto>> Update([FromBody] UpdateSettingsInput input, CancellationToken ct)
    {
        if (_tenantContext.TenantId is not int tenantId) return NotFound();

        if (!await IsOwnerAsync(tenantId, ct))
            return Forbid();

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return NotFound();

        var name = (input.Name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100)
            return BadRequest(new { error = "Name is required (max 100 characters)." });

        tenant.Name = name;
        tenant.Location = string.IsNullOrWhiteSpace(input.Location) ? null : input.Location.Trim();
        tenant.Units = input.Units;
        tenant.AlertEmailEnabled = input.AlertEmailEnabled;
        tenant.PublicProfileEnabled = input.PublicProfileEnabled;
        tenant.PublicContactEmail = string.IsNullOrWhiteSpace(input.PublicContactEmail) ? null : input.PublicContactEmail.Trim();

        // Plan gates: marketplace map pin + Stripe deposits are paid-tier
        // features. We silently strip the values rather than 402'ing the
        // whole save — that way the rest of the form (name, contact email,
        // etc.) saves cleanly, and the UI hides the inputs anyway. The 402
        // path is reserved for explicit cap hits (CanAddPublicListing).
        var depositRequested = Math.Clamp(input.PublicDepositPercent, 0, 100);
        if (depositRequested > 0 && !await _featureGate.IsEnabledAsync(AppFeature.StripeDeposits, ct))
        {
            depositRequested = 0;
        }
        tenant.PublicDepositPercent = depositRequested;

        var canPin = await _featureGate.IsEnabledAsync(AppFeature.MarketplaceMapPin, ct);
        // Only persist coordinates that are real lat/lng (-90..90, -180..180)
        // AND the plan allows the map pin feature; anything else clears the pin.
        tenant.PublicLatitude = canPin && input.PublicLatitude is double lat && lat >= -90 && lat <= 90 ? lat : null;
        tenant.PublicLongitude = canPin && input.PublicLongitude is double lng && lng >= -180 && lng <= 180 ? lng : null;
        tenant.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return new TenantSettingsDto(tenant.Id, tenant.Name, tenant.Slug, tenant.Location, tenant.Units, tenant.AlertEmailEnabled, tenant.PublicProfileEnabled, tenant.PublicContactEmail, tenant.PublicDepositPercent, tenant.CreatedAt, tenant.PublicLatitude, tenant.PublicLongitude);
    }

    private async Task<bool> IsOwnerAsync(int tenantId, CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return false;
        _tenantContext.BypassFilter = true;
        try
        {
            return await _db.TenantMembers.AnyAsync(
                m => m.TenantId == tenantId && m.UserId == user.Id && m.Role == TenantRole.Owner, ct);
        }
        finally { _tenantContext.BypassFilter = false; }
    }
}
