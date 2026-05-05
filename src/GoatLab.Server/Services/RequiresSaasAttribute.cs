using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GoatLab.Server.Services;

// Marks a controller/action that only exists in the hosted SaaS build —
// billing, the public marketplace, buyer accounts, plan-tier management.
// In OSS mode (Saas:Enabled=false) the route 404s so it doesn't leak as a
// dead clickable surface or confuse self-hosters with a "Stripe not
// configured" 500.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresSaasAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var mode = context.HttpContext.RequestServices.GetRequiredService<IAppMode>();
        if (!mode.IsSaas)
        {
            context.Result = new NotFoundResult();
        }
        return Task.CompletedTask;
    }
}
