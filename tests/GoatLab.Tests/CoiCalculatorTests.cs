using GoatLab.Server.Services.Pedigree;
using GoatLab.Shared.Models;

namespace GoatLab.Tests;

// Wright's COI tests. Each test builds a minimal pedigree and verifies the
// computed coefficient against the closed-form Wright value.
public class CoiCalculatorTests
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

    private static int AddGoat(TestDb db, string name, int? sireId = null, int? damId = null)
    {
        var g = new Goat
        {
            TenantId = TenantId,
            Name = name,
            SireId = sireId,
            DamId = damId,
        };
        db.Context.Goats.Add(g);
        db.Context.SaveChanges();
        return g.Id;
    }

    [Fact]
    public async Task Unrelated_parents_yield_zero()
    {
        using var db = NewDb();
        var sire = AddGoat(db, "Sire");
        var dam = AddGoat(db, "Dam");
        var focal = AddGoat(db, "Kid", sireId: sire, damId: dam);

        var calc = new CoiCalculator(db.Context);
        var result = await calc.ComputeAsync(focal);

        Assert.NotNull(result);
        Assert.Equal(0.0, result!.Coi, precision: 10);
        Assert.Empty(result.CommonAncestors);
    }

    [Fact]
    public async Task Half_sibs_offspring_is_one_eighth()
    {
        using var db = NewDb();
        var sharedSire = AddGoat(db, "SharedSire");
        var dam1 = AddGoat(db, "Dam1");
        var dam2 = AddGoat(db, "Dam2");
        var halfA = AddGoat(db, "HalfA", sireId: sharedSire, damId: dam1);
        var halfB = AddGoat(db, "HalfB", sireId: sharedSire, damId: dam2);
        var kid = AddGoat(db, "Kid", sireId: halfA, damId: halfB);

        var calc = new CoiCalculator(db.Context);
        var result = await calc.ComputeAsync(kid);

        // (1/2)^(1+1+1) = 0.125, single common ancestor (the shared sire).
        Assert.NotNull(result);
        Assert.Equal(0.125, result!.Coi, precision: 10);
        Assert.Single(result.CommonAncestors);
        Assert.Equal(sharedSire, result.CommonAncestors[0].GoatId);
    }

    [Fact]
    public async Task Full_sibs_offspring_is_one_quarter()
    {
        using var db = NewDb();
        var p = AddGoat(db, "P");
        var q = AddGoat(db, "Q");
        var sib1 = AddGoat(db, "Sib1", sireId: p, damId: q);
        var sib2 = AddGoat(db, "Sib2", sireId: p, damId: q);
        var kid = AddGoat(db, "Kid", sireId: sib1, damId: sib2);

        var calc = new CoiCalculator(db.Context);
        var result = await calc.ComputeAsync(kid);

        // 2 common ancestors (P, Q). Each contributes (1/2)^(1+1+1) = 0.125. Total 0.25.
        Assert.NotNull(result);
        Assert.Equal(0.25, result!.Coi, precision: 10);
        Assert.Equal(2, result.CommonAncestors.Count);
    }

    [Fact]
    public async Task Parent_x_offspring_is_one_quarter()
    {
        using var db = NewDb();
        var sire = AddGoat(db, "Sire");
        var origDam = AddGoat(db, "OrigDam");
        // Daughter inherits from sire and origDam, then is bred back to sire.
        var daughter = AddGoat(db, "Daughter", sireId: sire, damId: origDam);
        var kid = AddGoat(db, "Kid", sireId: sire, damId: daughter);

        var calc = new CoiCalculator(db.Context);
        var result = await calc.ComputeAsync(kid);

        // Common ancestor: Sire. n1=0 (sire→sire), n2=1 (daughter→sire).
        // Contribution: (1/2)^(0+1+1) = 0.25.
        Assert.NotNull(result);
        Assert.Equal(0.25, result!.Coi, precision: 10);
    }

    [Fact]
    public async Task Deep_great_great_great_grandparent_is_under_one_percent()
    {
        // Build two 3-link chains from sire and dam back to a single shared
        // great-great-great-grandparent A. n1=n2=3 → (1/2)^7 = 0.0078125.
        using var db = NewDb();
        var commonAncestor = AddGoat(db, "Founder");

        var sireGGSire = AddGoat(db, "S-GG-Sire", sireId: commonAncestor);
        var sireGSire = AddGoat(db, "S-G-Sire", sireId: sireGGSire);
        var sire = AddGoat(db, "Sire", sireId: sireGSire);

        var damGGSire = AddGoat(db, "D-GG-Sire", sireId: commonAncestor);
        var damGSire = AddGoat(db, "D-G-Sire", sireId: damGGSire);
        var dam = AddGoat(db, "Dam", sireId: damGSire);

        var kid = AddGoat(db, "Kid", sireId: sire, damId: dam);

        var calc = new CoiCalculator(db.Context);
        var result = await calc.ComputeAsync(kid);

        Assert.NotNull(result);
        Assert.Equal(0.0078125, result!.Coi, precision: 10);
        Assert.Single(result.CommonAncestors);
        Assert.Equal(commonAncestor, result.CommonAncestors[0].GoatId);
        Assert.Equal(3, result.CommonAncestors[0].SirePathLength);
        Assert.Equal(3, result.CommonAncestors[0].DamPathLength);
    }
}
