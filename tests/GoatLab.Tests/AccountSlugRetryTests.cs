using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Tests;

// AccountController's signup loop derives a unique tenant slug. Two parallel
// signups with the same farm name can both pass the AnyAsync pre-check, so
// the controller now retries on DbUpdateException (driven by the unique
// index on Tenant.Slug). These tests assert that index does the gate-keeping
// even if the retry logic ever regresses.
public class AccountSlugRetryTests
{
    [Fact]
    public async Task Tenant_slug_unique_index_rejects_duplicate_inserts()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();

        db.Context.Tenants.Add(new Tenant { Name = "Cedar", Slug = "cedar", PlanId = 1 });
        await db.Context.SaveChangesAsync();

        // Second insert with the same slug must throw — that's the signal the
        // signup retry catches and uses to bump the suffix.
        db.Context.Tenants.Add(new Tenant { Name = "Cedar (other)", Slug = "cedar", PlanId = 1 });
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Bumped_suffix_succeeds_after_collision()
    {
        // Mimics what the signup retry loop does: detach the failed entity,
        // bump the suffix, save again. If this stops working the controller
        // will start surfacing 500s on concurrent same-name signups.
        using var db = new TestDb();
        db.SeedDefaultPlans();

        db.Context.Tenants.Add(new Tenant { Name = "Cedar", Slug = "cedar", PlanId = 1 });
        await db.Context.SaveChangesAsync();

        var dup = new Tenant { Name = "Cedar (other)", Slug = "cedar", PlanId = 1 };
        db.Context.Tenants.Add(dup);
        try { await db.Context.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            db.Context.Entry(dup).State = EntityState.Detached;
            dup = new Tenant { Name = "Cedar (other)", Slug = "cedar-2", PlanId = 1 };
            db.Context.Tenants.Add(dup);
            await db.Context.SaveChangesAsync();
        }

        Assert.Equal(2, await db.Context.Tenants.CountAsync());
    }
}
