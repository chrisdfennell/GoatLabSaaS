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
    private readonly IAppMode _appMode;

    private Plan? _cachedPlan;
    private bool _cached;

    public FeatureGate(GoatLabDbContext db, ITenantContext tenantContext, IAppMode appMode)
    {
        _db = db;
        _tenantContext = tenantContext;
        _appMode = appMode;
    }

    public async Task<Plan?> GetCurrentPlanAsync(CancellationToken cancellationToken = default)
    {
        if (_cached) return _cachedPlan;
        _cached = true;

        // Self-host: synthesise an "all features, no caps" plan so callers
        // that read plan.MaxUsers / plan.MaxGoats see "unlimited" (null) and
        // every feature is on. Avoids needing to seed real Plan rows for OSS.
        if (_appMode.IsOss)
        {
            return _cachedPlan = SelfHostPlan;
        }

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

    private static readonly Plan SelfHostPlan = new()
    {
        Id = 0,
        Name = "Self-hosted",
        Slug = "self-hosted",
        IsActive = true,
        IsPublic = false,
        PriceMonthlyCents = 0,
        TrialDays = 0,
        MaxGoats = null,
        MaxUsers = null,
        MaxPublicListings = null,
        MaxPhotosPerGoat = null,
        Features = Enum.GetValues<AppFeature>()
            .Select(f => new PlanFeature { Feature = f, Enabled = true })
            .ToList(),
    };

    public async Task<bool> IsEnabledAsync(AppFeature feature, CancellationToken cancellationToken = default)
    {
        if (_appMode.IsOss) return true;
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

    public async Task<bool> CanAddPublicListingAsync(CancellationToken cancellationToken = default)
    {
        var plan = await GetCurrentPlanAsync(cancellationToken);
        if (plan is null) return false;
        if (plan.MaxPublicListings is not int cap) return true;

        if (_tenantContext.TenantId is not int tenantId) return false;
        // Count active for-sale, non-external goats. Tenant filter is on by
        // default at this point so the count is correctly scoped.
        var count = await _db.Goats.CountAsync(
            g => g.IsListedForSale && !g.IsExternal, cancellationToken);
        return count < cap;
    }

    public async Task<bool> CanAddPhotoAsync(int goatId, CancellationToken cancellationToken = default)
    {
        var plan = await GetCurrentPlanAsync(cancellationToken);
        if (plan is null) return false;
        if (plan.MaxPhotosPerGoat is not int cap) return true;

        // Photos table participates in the tenant filter via Goat → Tenant,
        // but we filter directly on GoatId so that's redundant scope-wise.
        var count = await _db.GoatPhotos.CountAsync(p => p.GoatId == goatId, cancellationToken);
        return count < cap;
    }
}
