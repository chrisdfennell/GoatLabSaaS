using GoatLab.Server.Data;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Plans;

// Scoped per request. Caches the loaded plan so repeated checks within one
// request don't re-query the DB.
public class FeatureGate : IFeatureGate
{
    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;

    private Plan? _cachedPlan;
    private bool _cached;

    public FeatureGate(GoatLabDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Plan?> GetCurrentPlanAsync(CancellationToken cancellationToken = default)
    {
        if (_cached) return _cachedPlan;
        _cached = true;

        if (_tenantContext.TenantId is not int tenantId)
            return _cachedPlan = null;

        _tenantContext.BypassFilter = true;
        try
        {
            var tenant = await _db.Tenants
                .Include(t => t.Plan).ThenInclude(p => p!.Features)
                .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
            return _cachedPlan = tenant?.Plan;
        }
        finally { _tenantContext.BypassFilter = false; }
    }

    public async Task<bool> IsEnabledAsync(AppFeature feature, CancellationToken cancellationToken = default)
    {
        var plan = await GetCurrentPlanAsync(cancellationToken);
        if (plan is null) return false;
        return plan.Features.Any(f => f.Feature == feature && f.Enabled);
    }

    public async Task<bool> CanAddGoatAsync(CancellationToken cancellationToken = default)
    {
        var plan = await GetCurrentPlanAsync(cancellationToken);
        if (plan is null) return false;
        if (plan.MaxGoats is not int cap) return true;

        if (_tenantContext.TenantId is not int tenantId) return false;
        var count = await _db.Goats.CountAsync(g => g.TenantId == tenantId, cancellationToken);
        return count < cap;
    }

    public async Task<bool> CanAddUserAsync(CancellationToken cancellationToken = default)
    {
        var plan = await GetCurrentPlanAsync(cancellationToken);
        if (plan is null) return false;
        if (plan.MaxUsers is not int cap) return true;

        if (_tenantContext.TenantId is not int tenantId) return false;
        _tenantContext.BypassFilter = true;
        try
        {
            var count = await _db.TenantMembers.CountAsync(m => m.TenantId == tenantId, cancellationToken);
            return count < cap;
        }
        finally { _tenantContext.BypassFilter = false; }
    }
}
