using GoatLab.Server.Services.Reports;
using GoatLab.Shared.Models;

namespace GoatLab.Tests;

// Covers the core aggregation logic of ReportsService. Tests use SQLite
// in-memory via TestDb. Date-window edge cases (inclusive bounds) + goat
// name lookups are exercised.
public class ReportsServiceTests
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

    private static int AddGoat(TestDb db, string name, GoatStatus status = GoatStatus.Healthy, DateTime? createdAt = null, DateTime? updatedAt = null)
    {
        var g = new Goat
        {
            TenantId = TenantId,
            Name = name,
            Status = status,
            CreatedAt = createdAt ?? DateTime.UtcNow.AddYears(-1),
            UpdatedAt = updatedAt ?? DateTime.UtcNow.AddYears(-1),
        };
        db.Context.Goats.Add(g);
        db.Context.SaveChanges();
        return g.Id;
    }

    [Fact]
    public async Task Pnl_sums_income_and_expenses_within_window()
    {
        using var db = NewDb();
        var goatId = AddGoat(db, "Clover");

        db.Context.Transactions.AddRange(
            new Transaction { TenantId = TenantId, Type = TransactionType.Income, Amount = 500m, Date = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc), Category = "AnimalSale", GoatId = goatId, Description = "sale" },
            new Transaction { TenantId = TenantId, Type = TransactionType.Expense, Amount = 120m, Date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), Category = "Feed", GoatId = goatId, Description = "hay" },
            // outside window
            new Transaction { TenantId = TenantId, Type = TransactionType.Expense, Amount = 9999m, Date = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc), Category = "Feed", Description = "excluded" }
        );
        db.Context.SaveChanges();

        var svc = new ReportsService(db.Context);
        var report = await svc.GetPnlAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        Assert.Equal(500m, report.Income);
        Assert.Equal(120m, report.Expenses);
        Assert.Equal(380m, report.Net);
        Assert.Single(report.CostPerGoat);
        Assert.Equal("Clover", report.CostPerGoat[0].GoatName);
        Assert.Equal(380m, report.CostPerGoat[0].Net);
    }

    [Fact]
    public async Task Milk_trends_totals_lbs_and_ranks_top_producers()
    {
        using var db = NewDb();
        var a = AddGoat(db, "Alpha");
        var b = AddGoat(db, "Bravo");

        db.Context.MilkLogs.AddRange(
            new MilkLog { TenantId = TenantId, GoatId = a, Date = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), Amount = 8 },
            new MilkLog { TenantId = TenantId, GoatId = a, Date = new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc), Amount = 9 },
            new MilkLog { TenantId = TenantId, GoatId = b, Date = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), Amount = 4 }
        );
        db.Context.SaveChanges();

        var svc = new ReportsService(db.Context);
        var report = await svc.GetMilkTrendsAsync(new DateTime(2026, 4, 1), new DateTime(2026, 4, 30));

        Assert.Equal(21, report.TotalLbs, precision: 6);
        Assert.Equal(2, report.TopProducers.Count);
        Assert.Equal("Alpha", report.TopProducers[0].GoatName);
        Assert.Equal(17, report.TopProducers[0].TotalLbs, precision: 6);
        Assert.Equal(2, report.TopProducers[0].DaysRecorded);
    }

    [Fact]
    public async Task Kidding_calculates_live_birth_rate_and_litter_breakdown()
    {
        using var db = NewDb();
        var doe = AddGoat(db, "Doe");
        var breeding = new BreedingRecord
        {
            TenantId = TenantId,
            DoeId = doe,
            BreedingDate = new DateTime(2025, 11, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        db.Context.BreedingRecords.Add(breeding);
        db.Context.SaveChanges();

        db.Context.KiddingRecords.AddRange(
            new KiddingRecord { TenantId = TenantId, BreedingRecordId = breeding.Id, KiddingDate = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc), KidsBorn = 2, KidsAlive = 2 },
            new KiddingRecord { TenantId = TenantId, BreedingRecordId = breeding.Id, KiddingDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), KidsBorn = 3, KidsAlive = 2 },
            new KiddingRecord { TenantId = TenantId, BreedingRecordId = breeding.Id, KiddingDate = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc), KidsBorn = 1, KidsAlive = 1 }
        );
        db.Context.SaveChanges();

        var svc = new ReportsService(db.Context);
        var report = await svc.GetKiddingAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        Assert.Equal(3, report.KiddingCount);
        Assert.Equal(6, report.KidsBorn);
        Assert.Equal(5, report.KidsAlive);
        Assert.Equal(1, report.KidsDied);
        Assert.Equal(5.0 / 6.0, report.LiveBirthRate, precision: 6);
        Assert.Equal(1, report.SingleCount);
        Assert.Equal(1, report.TwinCount);
        Assert.Equal(1, report.TripletPlusCount);
    }

    [Fact]
    public async Task Mortality_counts_deceased_within_window()
    {
        using var db = NewDb();
        var inWindow = AddGoat(db, "Lost", GoatStatus.Deceased,
            createdAt: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            updatedAt: new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc));
        AddGoat(db, "Alive", GoatStatus.Healthy,
            createdAt: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            updatedAt: new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        AddGoat(db, "Outside", GoatStatus.Deceased,
            createdAt: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            updatedAt: new DateTime(2025, 11, 1, 0, 0, 0, DateTimeKind.Utc));

        var svc = new ReportsService(db.Context);
        var report = await svc.GetMortalityAsync(new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));

        Assert.Equal(1, report.DeceasedCount);
        Assert.Single(report.Goats);
        Assert.Equal("Lost", report.Goats[0].GoatName);
        Assert.Contains(report.Monthly, m => m.Year == 2026 && m.Month == 2 && m.Count == 1);
    }

    [Fact]
    public async Task Parasite_flags_danger_zone_over_four()
    {
        using var db = NewDb();
        var goatId = AddGoat(db, "Violet");

        db.Context.FamachaScores.AddRange(
            new FamachaScore { TenantId = TenantId, GoatId = goatId, Date = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc), Score = 2 },
            new FamachaScore { TenantId = TenantId, GoatId = goatId, Date = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc), Score = 4 },
            new FamachaScore { TenantId = TenantId, GoatId = goatId, Date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), Score = 5 }
        );
        db.Context.SaveChanges();

        var svc = new ReportsService(db.Context);
        var report = await svc.GetParasiteAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        Assert.Equal(3, report.ScoreCount);
        Assert.Equal((2 + 4 + 5) / 3.0, report.AverageScore, precision: 6);
        Assert.Equal(2, report.DangerZoneCount);
        Assert.Single(report.WorstGoats);
        Assert.Equal(5, report.WorstGoats[0].LatestScore);
    }

    [Fact]
    public async Task HealthSpend_only_counts_veterinary_and_supplies_expenses()
    {
        using var db = NewDb();
        var goatId = AddGoat(db, "Buck");

        db.Context.Transactions.AddRange(
            new Transaction { TenantId = TenantId, Type = TransactionType.Expense, Amount = 75m, Date = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc), Category = "Veterinary", GoatId = goatId, Description = "vet call" },
            new Transaction { TenantId = TenantId, Type = TransactionType.Expense, Amount = 20m, Date = new DateTime(2026, 3, 6, 0, 0, 0, DateTimeKind.Utc), Category = "Supplies", GoatId = goatId, Description = "wormer" },
            // non-health category, excluded
            new Transaction { TenantId = TenantId, Type = TransactionType.Expense, Amount = 500m, Date = new DateTime(2026, 3, 7, 0, 0, 0, DateTimeKind.Utc), Category = "Feed", Description = "hay" },
            // income, excluded
            new Transaction { TenantId = TenantId, Type = TransactionType.Income, Amount = 300m, Date = new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc), Category = "AnimalSale", Description = "sale" }
        );
        db.Context.SaveChanges();

        var svc = new ReportsService(db.Context);
        var report = await svc.GetHealthSpendAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        Assert.Equal(95m, report.TotalSpend);
        Assert.Equal(2, report.ByCategory.Count);
        Assert.Single(report.ByGoat);
        Assert.Equal("Buck", report.ByGoat[0].GoatName);
        Assert.Equal(95m, report.ByGoat[0].Total);
    }
}
