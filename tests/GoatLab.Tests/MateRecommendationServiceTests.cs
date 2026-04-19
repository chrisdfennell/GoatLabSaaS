using GoatLab.Server.Services.Pedigree;
using GoatLab.Shared.Models;

namespace GoatLab.Tests;

public class MateRecommendationServiceTests
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

    private static int AddGoat(TestDb db, string name, Gender gender, int? sireId = null, int? damId = null, bool isExternal = false)
    {
        var g = new Goat
        {
            TenantId = TenantId,
            Name = name,
            Gender = gender,
            Status = GoatStatus.Healthy,
            SireId = sireId,
            DamId = damId,
            IsExternal = isExternal,
        };
        db.Context.Goats.Add(g);
        db.Context.SaveChanges();
        return g.Id;
    }

    [Fact]
    public async Task Unrelated_buck_outranks_full_sibling_buck()
    {
        using var db = NewDb();
        // External parents (IsExternal=true) so the service excludes them from the
        // candidate list — keeps this test focused on just the two in-herd bucks.
        var p = AddGoat(db, "P", Gender.Male, isExternal: true);
        var q = AddGoat(db, "Q", Gender.Female, isExternal: true);
        var doeId = AddGoat(db, "Doe", Gender.Female, sireId: p, damId: q);
        var unrelatedBuck = AddGoat(db, "UnrelatedBuck", Gender.Male);
        // Full sibling of the doe → mating them = COI of 0.25 (shared P + Q).
        var siblingBuck = AddGoat(db, "SiblingBuck", Gender.Male, sireId: p, damId: q);

        var svc = new MateRecommendationService(db.Context, new CoiCalculator(db.Context));
        var result = await svc.RecommendForDoeAsync(doeId);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Equal(unrelatedBuck, result[0].BuckId);
        Assert.Equal(siblingBuck, result[1].BuckId);
        Assert.True(result[0].CompositeScore > result[1].CompositeScore);
        Assert.Equal(0.0, result[0].ProjectedCoi, precision: 10);
        Assert.Equal(0.25, result[1].ProjectedCoi, precision: 10);
    }

    [Fact]
    public async Task External_bucks_are_excluded()
    {
        using var db = NewDb();
        var doeId = AddGoat(db, "Doe", Gender.Female);
        AddGoat(db, "ExternalBuck", Gender.Male, isExternal: true);
        var internalBuck = AddGoat(db, "InternalBuck", Gender.Male);

        var svc = new MateRecommendationService(db.Context, new CoiCalculator(db.Context));
        var result = await svc.RecommendForDoeAsync(doeId);

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal(internalBuck, result[0].BuckId);
    }

    [Fact]
    public async Task Female_focal_returns_null_when_focal_is_male()
    {
        using var db = NewDb();
        var notADoeId = AddGoat(db, "Buck", Gender.Male);
        AddGoat(db, "AnotherBuck", Gender.Male);

        var svc = new MateRecommendationService(db.Context, new CoiCalculator(db.Context));
        var result = await svc.RecommendForDoeAsync(notADoeId);

        Assert.Null(result);
    }

    [Fact]
    public async Task Proven_buck_production_bonus_beats_unproven_at_same_coi()
    {
        using var db = NewDb();
        var doeId = AddGoat(db, "Doe", Gender.Female);
        var provenBuck = AddGoat(db, "ProvenBuck", Gender.Male);
        var unprovenBuck = AddGoat(db, "UnprovenBuck", Gender.Male);

        // Give the proven buck a kidding history via a BreedingRecord.
        var someDoe = AddGoat(db, "SomeDoe", Gender.Female);
        var breeding = new BreedingRecord
        {
            TenantId = TenantId,
            DoeId = someDoe,
            BuckId = provenBuck,
            BreedingDate = DateTime.UtcNow.AddDays(-200),
            Outcome = BreedingOutcome.Confirmed,
        };
        db.Context.BreedingRecords.Add(breeding);
        db.Context.SaveChanges();

        db.Context.KiddingRecords.Add(new KiddingRecord
        {
            TenantId = TenantId,
            BreedingRecordId = breeding.Id,
            KiddingDate = DateTime.UtcNow.AddDays(-50),
            KidsBorn = 2,
            KidsAlive = 2,
        });
        db.Context.SaveChanges();

        var svc = new MateRecommendationService(db.Context, new CoiCalculator(db.Context));
        var result = await svc.RecommendForDoeAsync(doeId);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        // Both have COI=0 (unrelated). Proven buck should win on production signals.
        Assert.Equal(provenBuck, result[0].BuckId);
        Assert.Equal(unprovenBuck, result[1].BuckId);
        Assert.Equal(1, result[0].KiddingsSired);
        Assert.Equal(0, result[1].KiddingsSired);
        Assert.True(result[0].CompositeScore > result[1].CompositeScore);
    }
}
