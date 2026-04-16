using Hangfire.Dashboard;

namespace GoatLab.Server.Services.Jobs;

// Gates the Hangfire dashboard behind the super_admin claim. Called on every
// dashboard request so Blazor/cookie auth state is already resolved by the
// time we get here.
public class HangfireSuperAdminFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.HasClaim(SuperAdminPolicy.ClaimType, "true");
    }
}
