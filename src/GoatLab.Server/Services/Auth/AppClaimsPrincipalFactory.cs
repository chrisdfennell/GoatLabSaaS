using System.Security.Claims;
using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoatLab.Server.Services.Auth;

// Custom claims factory: ensures super_admin (and tenant_id when only one
// membership exists) survive cookie renewals. The default factory only emits
// Identity's built-in claims (nameidentifier/name/email/SecurityStamp). The
// SecurityStampValidator periodically regenerates the principal via this
// factory — without this override, custom claims added at sign-in via
// SignInWithClaimsAsync get silently stripped, and admin endpoints start
// returning 403 ~30 minutes after login.
//
// Tenant resolution: if the user has exactly one active tenant membership,
// stamp tenant_id automatically. Otherwise leave it off and let the user
// pick via /select-farm (which re-signs in with the chosen tenant).
public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AppClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options,
        GoatLabDbContext db,
        ITenantContext tenantContext)
        : base(userManager, roleManager, options)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (user.IsSuperAdmin)
            identity.AddClaim(new Claim(SuperAdminPolicy.ClaimType, "true"));

        // Auto-stamp tenant_id only if the user has exactly one active
        // membership. Multi-tenant users have a /select-farm flow that
        // re-signs them in with the chosen tenant.
        _tenantContext.BypassFilter = true;
        try
        {
            var memberships = await _db.TenantMembers
                .AsNoTracking()
                .Where(m => m.UserId == user.Id
                            && m.Tenant!.DeletedAt == null
                            && m.Tenant.SuspendedAt == null)
                .Select(m => m.TenantId)
                .Take(2)
                .ToListAsync();

            if (memberships.Count == 1)
                identity.AddClaim(new Claim("tenant_id", memberships[0].ToString()));
        }
        finally
        {
            _tenantContext.BypassFilter = false;
        }

        return identity;
    }
}
