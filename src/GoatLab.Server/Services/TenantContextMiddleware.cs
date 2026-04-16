using System.Security.Claims;

namespace GoatLab.Server.Services;

/// <summary>
/// Reads the authenticated user's current tenant id from claims and populates
/// the request-scoped <see cref="ITenantContext"/>. If the user has no tenant
/// claim (unauthenticated, signup in progress, or picker not answered yet),
/// TenantId stays null and EF query filters exclude all tenant-scoped data.
/// </summary>
public class TenantContextMiddleware
{
    public const string TenantClaimType = "tenant_id";

    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claim = context.User.FindFirstValue(TenantClaimType);
            if (int.TryParse(claim, out var tid) && tenantContext is TenantContext tc)
            {
                tc.TenantId = tid;
            }
        }

        await _next(context);
    }
}
