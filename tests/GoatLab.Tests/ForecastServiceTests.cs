using GoatLab.Server.Services.Reports;
using GoatLab.Shared.Models;

namespace GoatLab.Tests;

// ForecastService tests — exercise horizon buckets for kidding, trailing-average
// behavior for milk, and simple daily-averaged projection for cash flow.
public class ForecastServiceTests
{
    private const int TenantId = 1;

    private static TestDb NewDb()
    {
        var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant { Id = TenantId, Name = "Acme", Slug = "acme", PlanId = 3 });
        db.Context.SaveChanges();
        db.Tenant.TenantId = TenantId;
        return db;
    }

    private static int AddGoat(TestDb db, string name)
    {
        var g = new Goat { TenantId = TenantId, Name = name };
        db.Context.Goats.Add(g);
        db.Context.SaveChanges();
        return g.Id;
    }

    [Fact]
    public async Task Kidding_forecast_buckets_due_dates_into_horizons()
    {
        using var db = NewDb();
        var doe = AddGoat(db, "Daisy");
        var today = DateTime.UtcNow.Date;

        db.Context.BreedingRecords.AddRange(
            new BreedingRecord { TenantId = TenantId, DoeId = doe, BreedingDate = today.AddDays(-100), EstimatedDueDate = today.AddDays(15) },
            new BreedingRecord { TenantId = TenantId, DoeId = doe, BreedingDate = today.AddDays(-80),  EstimatedDueDate = today.AddDays(45) },
            new BreedingRecord { TenantId = TenantId, DoeId = doe, BreedingDate = today.AddDays(-60),  EstimatedDueDate = today.AddDays(75) },
            // Outside 90-day horizon
            new BreedingRecord { TenantId = TenantId, DoeId = doe, BreedingDate = today.AddDays(-30),  EstimatedDueDate = today.AddDays(120) },
            // Already kidded — should be excluded via KiddingRecords check
            new BreedingRecord { TenantId = TenantId, DoeId = doe, BreedingDate = today.AddDays(-160), EstimatedDueDate = today.AddDays(-10), Outcome = BreedingOutcome.Confirmed }
        );
        db.Context.SaveChanges();

        var svc = new ForecastService(db.Context);
        var f = await svc.GetKiddingForecastAsync(90);

        Assert.Equal(1, f.Horizon30);
        Assert.Equal(2, f.Horizon60);
        Assert.Equal(3, f.Horizon90);
        Assert.Equal(3, f.Upcoming.Count);
    }

    [Fact]
    public async Task Milk_forecast_projects_trailing_14day_average()
    {
        using var db = NewDb();
        var doe = AddGoat(db, "Flora");
        var today = DateTime.UtcNow.Date;

        // 7 lbs/day for the trailing 14 days = 98 lbs total, avg 7.
        for (int i = 1; i <= 14; i++)
        {
            db.Context.MilkLogs.Add(new MilkLog
            {
                TenantId = TenantId,
                GoatId = doe,
                Date = today.AddDays(-i),
                Amount = 7
            });
        }
        db.Context.SaveChanges();

        var svc = new ForecastService(db.Context);
        var f = await svc.GetMilkForecastAsync(30);

        Assert.Equal(7.0, f.TrailingDailyAverage, precision: 6);
        Assert.Equal(7.0 * 30, f.ProjectedTotal, precision: 6);
        Assert.Equal(30, f.Projected.Count);
        Assert.All(f.Projected, d => Assert.Equal(7.0, d.Lbs, precision: 6));
    }

    [Fact]
    public async Task Cashflow_forecast_projects_net_from_trailing_90day_totals()
    {
        using var db = NewDb();
        var today = DateTime.UtcNow.Date;

        // 90 days of $10/day income and $4/day expense → $6/day net.
        for (int i = 1; i <= 90; i++)
        {
            db.Context.Transactions.AddRange(
                new Transaction { TenantId = TenantId, Type = TransactionType.Income, Amount = 10m, Date = today.AddDays(-i), Description = "inc" },
                new Transaction { TenantId = TenantId, Type = TransactionType.Expense, Amount = 4m, Date = today.AddDays(-i), Description = "exp" }
            );
        }
        db.Context.SaveChanges();

        var svc = new ForecastService(db.Context);
        var f = await svc.GetCashflowForecastAsync(30);

        Assert.Equal(10m, f.TrailingIncomeDaily);
        Assert.Equal(4m, f.TrailingExpenseDaily);
        Assert.Equal(300m, f.ProjectedIncome);
        Assert.Equal(120m, f.ProjectedExpense);
        Assert.Equal(180m, f.ProjectedNet);
        Assert.Equal(30, f.Projected.Count);
        Assert.Equal(180m, f.Projected[^1].CumulativeNet);
    }
}
