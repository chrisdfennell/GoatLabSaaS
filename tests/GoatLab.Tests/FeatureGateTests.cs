using GoatLab.Server.Services.Plans;
using GoatLab.Shared.Models;

namespace GoatLab.Tests;

// FeatureGate is the linchpin of all plan enforcement. These tests pin down
// the three jobs it does: lookup the plan, answer IsEnabled, and enforce caps.
public class FeatureGateTests
{
    [Fact]
    public async Task GetCurrentPlanAsync_returns_null_when_no_tenant_context()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Tenant.TenantId = null;

        var gate = new FeatureGate(db.Context, db.Tenant);
        Assert.Null(await gate.GetCurrentPlanAsync());
    }

    [Fact]
    public async Task GetCurrentPlanAsync_returns_tenant_plan_with_features()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant { Id = 1, Name = "Acme", Slug = "acme", PlanId = 1 });
        db.Context.SaveChanges();
        db.Tenant.TenantId = 1;

        var gate = new FeatureGate(db.Context, db.Tenant);
        var plan = await gate.GetCurrentPlanAsync();

        Assert.NotNull(plan);
        Assert.Equal("Homestead", plan!.Name);
        Assert.NotEmpty(plan.Features);
    }

    [Fact]
    public async Task IsEnabledAsync_respects_plan_features()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant { Id = 1, Name = "Acme", Slug = "acme", PlanId = 1 }); // Homestead
        db.Context.SaveChanges();
        db.Tenant.TenantId = 1;

        var gate = new FeatureGate(db.Context, db.Tenant);

        Assert.True(await gate.IsEnabledAsync(AppFeature.Goats));
        Assert.True(await gate.IsEnabledAsync(AppFeature.Health));
        Assert.False(await gate.IsEnabledAsync(AppFeature.Sales));        // Homestead excludes Sales
        Assert.False(await gate.IsEnabledAsync(AppFeature.Finance));
    }

    [Fact]
    public async Task CanAddGoatAsync_true_when_under_cap()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant { Id = 1, Name = "Acme", Slug = "acme", PlanId = 1 }); // cap 10
        db.Context.SaveChanges();
        db.Tenant.TenantId = 1;

        // Seed 9 goats
        for (int i = 0; i < 9; i++)
            db.Context.Goats.Add(new Goat { Name = $"G{i}", TenantId = 1 });
        db.Context.SaveChanges();

        var gate = new FeatureGate(db.Context, db.Tenant);
        Assert.True(await gate.CanAddGoatAsync());
    }

    [Fact]
    public async Task CanAddGoatAsync_false_at_cap()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant { Id = 1, Name = "Acme", Slug = "acme", PlanId = 1 });
        db.Context.SaveChanges();
        db.Tenant.TenantId = 1;

        for (int i = 0; i < 10; i++)
            db.Context.Goats.Add(new Goat { Name = $"G{i}", TenantId = 1 });
        db.Context.SaveChanges();

        var gate = new FeatureGate(db.Context, db.Tenant);
        Assert.False(await gate.CanAddGoatAsync());
    }

    [Fact]
    public async Task CanAddGoatAsync_true_on_unlimited_plan()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant { Id = 1, Name = "BigFarm", Slug = "bigfarm", PlanId = 2 }); // Farm = unlimited
        db.Context.SaveChanges();
        db.Tenant.TenantId = 1;

        for (int i = 0; i < 500; i++)
            db.Context.Goats.Add(new Goat { Name = $"G{i}", TenantId = 1 });
        db.Context.SaveChanges();

        var gate = new FeatureGate(db.Context, db.Tenant);
        Assert.True(await gate.CanAddGoatAsync());
    }
}
