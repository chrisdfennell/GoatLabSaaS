using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services.Jobs;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoatLab.Tests;

// HardDeleteSweepJob enforces the 30-day GDPR promise from the account-
// deletion flow. Miss the cutoff logic and we either delete data early
// (data loss) or never delete (broken promise).
public class HardDeleteSweepJobTests
{
    [Fact]
    public async Task Deletes_user_soft_deleted_over_30_days_ago()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();

        var old = new ApplicationUser
        {
            Id = "u-old", UserName = "old@x.com", NormalizedUserName = "OLD@X.COM",
            Email = "old@x.com", NormalizedEmail = "OLD@X.COM",
            DisplayName = "Old", DeletedAt = DateTime.UtcNow.AddDays(-31),
        };
        db.Context.Users.Add(old);
        await db.Context.SaveChangesAsync();

        var job = new HardDeleteSweepJob(db.Context, db.BuildUserManager(),
            NullLogger<HardDeleteSweepJob>.Instance);
        await job.RunAsync(CancellationToken.None);

        Assert.Null(await db.Context.Users.FirstOrDefaultAsync(u => u.Id == "u-old"));
    }

    [Fact]
    public async Task Keeps_user_soft_deleted_within_30_days()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();

        var fresh = new ApplicationUser
        {
            Id = "u-fresh", UserName = "fresh@x.com", NormalizedUserName = "FRESH@X.COM",
            Email = "fresh@x.com", NormalizedEmail = "FRESH@X.COM",
            DisplayName = "Fresh", DeletedAt = DateTime.UtcNow.AddDays(-5),
        };
        db.Context.Users.Add(fresh);
        await db.Context.SaveChangesAsync();

        var job = new HardDeleteSweepJob(db.Context, db.BuildUserManager(),
            NullLogger<HardDeleteSweepJob>.Instance);
        await job.RunAsync(CancellationToken.None);

        Assert.NotNull(await db.Context.Users.FirstOrDefaultAsync(u => u.Id == "u-fresh"));
    }

    [Fact]
    public async Task Keeps_active_user_with_no_deleted_at()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();

        var active = new ApplicationUser
        {
            Id = "u-active", UserName = "active@x.com", NormalizedUserName = "ACTIVE@X.COM",
            Email = "active@x.com", NormalizedEmail = "ACTIVE@X.COM",
            DisplayName = "Active",
        };
        db.Context.Users.Add(active);
        await db.Context.SaveChangesAsync();

        var job = new HardDeleteSweepJob(db.Context, db.BuildUserManager(),
            NullLogger<HardDeleteSweepJob>.Instance);
        await job.RunAsync(CancellationToken.None);

        Assert.NotNull(await db.Context.Users.FirstOrDefaultAsync(u => u.Id == "u-active"));
    }

    [Fact]
    public async Task Deletes_tenant_soft_deleted_over_30_days_ago()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant
        {
            Id = 1, Name = "OldFarm", Slug = "old-farm", PlanId = 1,
            DeletedAt = DateTime.UtcNow.AddDays(-45),
        });
        await db.Context.SaveChangesAsync();

        var job = new HardDeleteSweepJob(db.Context, db.BuildUserManager(),
            NullLogger<HardDeleteSweepJob>.Instance);
        await job.RunAsync(CancellationToken.None);

        db.Tenant.BypassFilter = true;
        Assert.Null(await db.Context.Tenants.FirstOrDefaultAsync(t => t.Id == 1));
    }
}
