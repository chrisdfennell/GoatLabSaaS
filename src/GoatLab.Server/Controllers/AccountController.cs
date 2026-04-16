using System.Security.Claims;
using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        GoatLabDbContext db,
        ITenantContext tenantContext)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _tenantContext = tenantContext;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            DisplayName = req.DisplayName,
        };

        var createResult = await _userManager.CreateAsync(user, req.Password);
        if (!createResult.Succeeded)
            return BadRequest(new { errors = createResult.Errors.Select(e => e.Description) });

        // Bypass the tenant query filter while creating the user's first tenant.
        _tenantContext.BypassFilter = true;

        var slug = Slugify(req.FarmName);
        var baseSlug = slug;
        var n = 1;
        while (await _db.Tenants.AnyAsync(t => t.Slug == slug))
        {
            slug = $"{baseSlug}-{++n}";
        }

        var tenant = new Tenant { Name = req.FarmName, Slug = slug };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        _db.TenantMembers.Add(new TenantMember
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = TenantRole.Owner,
        });
        await _db.SaveChangesAsync();

        // Sign the user in with a tenant_id claim so subsequent requests are scoped.
        await _signInManager.SignInWithClaimsAsync(user, isPersistent: true, BuildClaims(user, tenant.Id));

        return Ok(await BuildCurrentUserDto(user));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user is null) return Unauthorized(new { error = "Invalid email or password." });

        if (user.DeletedAt is not null)
            return Unauthorized(new { error = "Account is disabled. Contact support." });

        var check = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (check.IsLockedOut)
            return Unauthorized(new { error = "Account is locked. Try again later or contact support." });
        if (!check.Succeeded) return Unauthorized(new { error = "Invalid email or password." });

        // Pick a default tenant: if only one membership, use it. Otherwise, log
        // the user in with no tenant claim; client will call /select-tenant.
        // Filter out suspended/deleted tenants — they can't be selected.
        _tenantContext.BypassFilter = true;
        var memberships = await _db.TenantMembers
            .Where(m => m.UserId == user.Id)
            .Include(m => m.Tenant)
            .Where(m => m.Tenant!.DeletedAt == null && m.Tenant!.SuspendedAt == null)
            .ToListAsync();

        int? tenantId = memberships.Count == 1 ? memberships[0].TenantId : (int?)null;

        await _signInManager.SignInWithClaimsAsync(user, req.RememberMe, BuildClaims(user, tenantId));
        return Ok(await BuildCurrentUserDto(user));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return NoContent();
    }

    [Authorize]
    [HttpPost("select-tenant")]
    public async Task<IActionResult> SelectTenant(SelectTenantRequest req)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        _tenantContext.BypassFilter = true;
        var membership = await _db.TenantMembers
            .Include(m => m.Tenant)
            .FirstOrDefaultAsync(m => m.UserId == user.Id && m.TenantId == req.TenantId);
        if (membership is null) return Forbid();
        if (membership.Tenant?.DeletedAt is not null)
            return BadRequest(new { error = "This farm has been deleted." });
        if (membership.Tenant?.SuspendedAt is not null)
            return BadRequest(new { error = "This farm is suspended. Contact support." });

        await _signInManager.SignInWithClaimsAsync(user, isPersistent: true, BuildClaims(user, req.TenantId));
        return Ok(await BuildCurrentUserDto(user));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        return Ok(await BuildCurrentUserDto(user));
    }

    private async Task<CurrentUserDto> BuildCurrentUserDto(ApplicationUser user)
    {
        _tenantContext.BypassFilter = true;
        var memberships = await _db.TenantMembers
            .Where(m => m.UserId == user.Id)
            .Include(m => m.Tenant)
            .Where(m => m.Tenant!.DeletedAt == null)
            .Select(m => new TenantMembershipDto(m.TenantId, m.Tenant!.Name, m.Tenant!.Slug, m.Role))
            .ToListAsync();

        int? currentTenantId = null;
        var claim = User.FindFirstValue(TenantContextMiddleware.TenantClaimType);
        if (int.TryParse(claim, out var tid)) currentTenantId = tid;

        return new CurrentUserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            currentTenantId,
            memberships,
            user.IsSuperAdmin);
    }

    private static IEnumerable<Claim> BuildClaims(ApplicationUser user, int? tenantId)
    {
        if (tenantId is int tid)
            yield return new Claim(TenantContextMiddleware.TenantClaimType, tid.ToString());
        if (user.IsSuperAdmin)
            yield return new Claim(SuperAdminPolicy.ClaimType, "true");
    }

    private static string Slugify(string name)
    {
        var s = new string(name.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray());
        while (s.Contains("--")) s = s.Replace("--", "-");
        return s.Trim('-');
    }
}
