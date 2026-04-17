using GoatLab.Server.Services.Alerts;
using GoatLab.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoatLab.Tests;

// AlertScannerService is the heart of SmartAlerts: it scans tenant data and
// materializes Alert rows. These tests pin down each source path + idempotency.
public class AlertScannerServiceTests
{
    private static (TestDb db, Goat goat) SeedGoat(int tenantId = 1)
    {
        var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant { Id = tenantId, Name = "Acme", Slug = "acme", PlanId = 3 });
        db.Context.SaveChanges();
        db.Tenant.TenantId = tenantId;

        var goat = new Goat { Name = "Daisy", TenantId = tenantId };
        db.Context.Goats.Add(goat);
        db.Context.SaveChanges();
        return (db, goat);
    }

    [Fact]
    public async Task Materializes_one_alert_per_overdue_medical_record()
    {
        var (db, goat) = SeedGoat();
        db.Context.MedicalRecords.Add(new MedicalRecord
        {
            TenantId = 1,
            GoatId = goat.Id,
            Title = "CDT booster",
            RecordType = MedicalRecordType.Vaccination,
            Date = DateTime.UtcNow.AddMonths(-6),
            NextDueDate = DateTime.UtcNow.AddDays(-2),
        });
        db.Context.SaveChanges();

        var scanner = new AlertScannerService(db.Context, NullLogger<AlertScannerService>.Instance);
        var fresh = await scanner.ScanTenantAsync(1);

        Assert.Single(fresh);
        Assert.Equal(AlertType.MedicationOverdue, fresh[0].Type);
        Assert.Equal(AlertSeverity.Error, fresh[0].Severity);
        Assert.Equal("MedicalRecord", fresh[0].EntityType);
    }

    [Fact]
    public async Task Second_scan_is_idempotent_when_nothing_changes()
    {
        var (db, goat) = SeedGoat();
        db.Context.MedicalRecords.Add(new MedicalRecord
        {
            TenantId = 1, GoatId = goat.Id, Title = "Booster",
            Date = DateTime.UtcNow.AddDays(-30), NextDueDate = DateTime.UtcNow.AddDays(-1),
        });
        db.Context.SaveChanges();

        var scanner = new AlertScannerService(db.Context, NullLogger<AlertScannerService>.Instance);
        await scanner.ScanTenantAsync(1);
        var second = await scanner.ScanTenantAsync(1);

        Assert.Empty(second);
        Assert.Equal(1, db.Context.Alerts.Count());
    }

    [Fact]
    public async Task Detects_weight_drop_over_threshold()
    {
        var (db, goat) = SeedGoat();
        db.Context.WeightRecords.AddRange(
            new WeightRecord { TenantId = 1, GoatId = goat.Id, Weight = 100, Date = DateTime.UtcNow.AddDays(-30) },
            new WeightRecord { TenantId = 1, GoatId = goat.Id, Weight = 85,  Date = DateTime.UtcNow.AddDays(-1) }); // 15% drop
        db.Context.SaveChanges();

        var scanner = new AlertScannerService(db.Context, NullLogger<AlertScannerService>.Instance);
        var fresh = await scanner.ScanTenantAsync(1);

        Assert.Contains(fresh, a => a.Type == AlertType.WeightDrop);
    }

    [Fact]
    public async Task Skips_weight_drop_under_threshold()
    {
        var (db, goat) = SeedGoat();
        db.Context.WeightRecords.AddRange(
            new WeightRecord { TenantId = 1, GoatId = goat.Id, Weight = 100, Date = DateTime.UtcNow.AddDays(-30) },
            new WeightRecord { TenantId = 1, GoatId = goat.Id, Weight = 95,  Date = DateTime.UtcNow.AddDays(-1) }); // 5% drop
        db.Context.SaveChanges();

        var scanner = new AlertScannerService(db.Context, NullLogger<AlertScannerService>.Instance);
        var fresh = await scanner.ScanTenantAsync(1);

        Assert.DoesNotContain(fresh, a => a.Type == AlertType.WeightDrop);
    }

    [Fact]
    public async Task Detects_low_feed_stock()
    {
        var (db, _) = SeedGoat();
        db.Context.FeedInventory.Add(new FeedInventory
        {
            TenantId = 1, FeedName = "Alfalfa pellets",
            QuantityOnHand = 4, LowStockThreshold = 10, Unit = "bags",
        });
        db.Context.SaveChanges();

        var scanner = new AlertScannerService(db.Context, NullLogger<AlertScannerService>.Instance);
        var fresh = await scanner.ScanTenantAsync(1);

        Assert.Contains(fresh, a => a.Type == AlertType.LowFeedStock);
    }

    [Fact]
    public async Task Detects_overdue_kidding_only_when_no_kidding_recorded()
    {
        var (db, _) = SeedGoat();
        var doe = new Goat { Name = "Bessie", TenantId = 1 };
        db.Context.Goats.Add(doe);
        db.Context.SaveChanges();

        db.Context.BreedingRecords.Add(new BreedingRecord
        {
            TenantId = 1, DoeId = doe.Id,
            BreedingDate = DateTime.UtcNow.AddDays(-160),
            EstimatedDueDate = DateTime.UtcNow.AddDays(-10),
            Outcome = BreedingOutcome.Pending,
        });
        db.Context.SaveChanges();

        var scanner = new AlertScannerService(db.Context, NullLogger<AlertScannerService>.Instance);
        var fresh = await scanner.ScanTenantAsync(1);

        Assert.Contains(fresh, a => a.Type == AlertType.KiddingOverdue);
    }
}
