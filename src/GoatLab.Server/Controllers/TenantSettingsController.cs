using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services;
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

    public TenantSettingsController(
        GoatLabDbContext db,
        ITenantContext tenantContext,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userManager = userManager;
    }

    public record TenantSettingsDto(
        int Id,
        string Name,
        string Slug,
        string? Location,
        TenantUnits Units,
        bool AlertEmailEnabled,
        DateTime CreatedAt);

    public record UpdateSettingsInput(
        string Name,
        string? Location,
        TenantUnits Units,
        bool AlertEmailEnabled);

    [HttpGet]
    public async Task<ActionResult<TenantSettingsDto>> Get(CancellationToken ct)
    {
        if (_tenantContext.TenantId is not int tenantId) return NotFound();
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return NotFound();
        return new TenantSettingsDto(tenant.Id, tenant.Name, tenant.Slug, tenant.Location, tenant.Units, tenant.AlertEmailEnabled, tenant.CreatedAt);
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
        tenant.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return new TenantSettingsDto(tenant.Id, tenant.Name, tenant.Slug, tenant.Location, tenant.Units, tenant.AlertEmailEnabled, tenant.CreatedAt);
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
