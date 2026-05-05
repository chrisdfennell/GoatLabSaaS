using GoatLab.Server.Services;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoatLab.Tests;

// Pins down the behavior of the SaaS-vs-OSS mode flag and the [RequiresSaas]
// authorization filter that 404s SaaS-only routes when the deploy is OSS.
public class AppModeTests
{
    private static IConfiguration ConfigWith(bool? saasEnabled)
    {
        var dict = new Dictionary<string, string?>();
        if (saasEnabled.HasValue) dict["Saas:Enabled"] = saasEnabled.Value.ToString();
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void AppMode_defaults_to_SaaS_when_config_missing()
    {
        IAppMode mode = new AppMode(ConfigWith(null));
        Assert.True(mode.IsSaas);
        Assert.False(mode.IsOss);
    }

    [Fact]
    public void AppMode_reads_explicit_false_as_OSS()
    {
        IAppMode mode = new AppMode(ConfigWith(false));
        Assert.False(mode.IsSaas);
        Assert.True(mode.IsOss);
    }

    [Fact]
    public void AppMode_reads_explicit_true_as_SaaS()
    {
        IAppMode mode = new AppMode(ConfigWith(true));
        Assert.True(mode.IsSaas);
    }

    [Fact]
    public async Task RequiresSaas_404s_when_OSS()
    {
        var ctx = BuildContext(new TestAppMode(isSaas: false));
        await new RequiresSaasAttribute().OnAuthorizationAsync(ctx);
        Assert.IsType<NotFoundResult>(ctx.Result);
    }

    [Fact]
    public async Task RequiresSaas_passes_through_when_SaaS()
    {
        var ctx = BuildContext(new TestAppMode(isSaas: true));
        await new RequiresSaasAttribute().OnAuthorizationAsync(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task FeatureGate_in_OSS_returns_all_features_enabled()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        // Note: NO tenant context. In OSS mode we still expect everything on.
        var gate = new FeatureGate(db.Context, db.Tenant, new TestAppMode(isSaas: false));

        foreach (var feature in Enum.GetValues<AppFeature>())
        {
            Assert.True(await gate.IsEnabledAsync(feature),
                $"OSS mode should report {feature} enabled, got false.");
        }
    }

    [Fact]
    public async Task FeatureGate_in_OSS_returns_synthetic_no_caps_plan()
    {
        using var db = new TestDb();
        var gate = new FeatureGate(db.Context, db.Tenant, new TestAppMode(isSaas: false));

        var plan = await gate.GetCurrentPlanAsync();
        Assert.NotNull(plan);
        Assert.Null(plan!.MaxGoats);
        Assert.Null(plan.MaxUsers);
        Assert.Null(plan.MaxPublicListings);
        Assert.Null(plan.MaxPhotosPerGoat);
        Assert.True(plan.Features.All(f => f.Enabled));
        Assert.Equal(Enum.GetValues<AppFeature>().Length, plan.Features.Count);
    }

    [Fact]
    public async Task FeatureGate_in_OSS_caps_pass_unconditionally()
    {
        using var db = new TestDb();
        // No tenant; in SaaS mode this would short-circuit to false on caps,
        // but in OSS mode the synthetic plan has null caps so all return true.
        var gate = new FeatureGate(db.Context, db.Tenant, new TestAppMode(isSaas: false));

        Assert.True(await gate.CanAddGoatAsync());
        Assert.True(await gate.CanAddUserAsync());
        Assert.True(await gate.CanAddPublicListingAsync());
        Assert.True(await gate.CanAddPhotoAsync(goatId: 1));
    }

    private static AuthorizationFilterContext BuildContext(IAppMode mode)
    {
        var http = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddSingleton(mode);
        http.RequestServices = services.BuildServiceProvider();

        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }
}
