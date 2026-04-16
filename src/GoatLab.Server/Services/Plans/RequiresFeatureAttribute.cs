using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GoatLab.Server.Services.Plans;

// Usage: [RequiresFeature(AppFeature.Sales)] on a controller or action.
// Returns HTTP 402 Payment Required when the tenant's plan doesn't enable
// the feature. 402 is the conventional signal for "upgrade needed" — lets the
// client show an upgrade prompt instead of treating it as a generic auth fail.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class RequiresFeatureAttribute : TypeFilterAttribute
{
    public RequiresFeatureAttribute(AppFeature feature) : base(typeof(RequiresFeatureFilter))
    {
        Arguments = new object[] { feature };
    }
}

public class RequiresFeatureFilter : IAsyncActionFilter
{
    private readonly AppFeature _feature;
    private readonly IFeatureGate _gate;

    public RequiresFeatureFilter(AppFeature feature, IFeatureGate gate)
    {
        _feature = feature;
        _gate = gate;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!await _gate.IsEnabledAsync(_feature))
        {
            context.Result = new ObjectResult(new
            {
                error = $"Your plan doesn't include {_feature}.",
                feature = _feature.ToString(),
                upgradeRequired = true,
            })
            {
                StatusCode = StatusCodes.Status402PaymentRequired,
            };
            return;
        }

        await next();
    }
}
