using GoatLab.Server.Controllers;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Tests;

// Regression tests for the IDOR fix that swapped FindAsync(id) → FirstOrDefaultAsync.
// FindAsync bypasses EF Core's global query filter; FirstOrDefaultAsync respects it.
// These tests confirm a tenant can't read or mutate another tenant's rows by ID.
public class TenantFilterRegressionTests
{
    private const int TenantA = 1;
    private const int TenantB = 2;

    private static TestDb NewDbWithTwoTenants()
    {
        var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.AddRange(
            new Tenant { Id = TenantA, Name = "Farm A", Slug = "farm-a", PlanId = 3 },
            new Tenant { Id = TenantB, Name = "Farm B", Slug = "farm-b", PlanId = 3 });
        db.Context.SaveChanges();
        return db;
    }

    [Fact]
    public async Task AlertsController_MarkRead_returns_NotFound_for_other_tenants_alert()
    {
        using var db = NewDbWithTwoTenants();

        // Create an alert in Tenant A.
        db.Tenant.TenantId = TenantA;
        var alert = new Alert
        {
            TenantId = TenantA,
            Type = AlertType.MedicationDue,
            Severity = AlertSeverity.Warning,
            Title = "Test",
            Body = "...",
        };
        db.Context.Alerts.Add(alert);
        await db.Context.SaveChangesAsync();

        // Switch to Tenant B and try to mark Tenant A's alert as read.
        db.Tenant.TenantId = TenantB;
        var ctrl = new AlertsController(db.Context);
        var result = await ctrl.MarkRead(alert.Id);

        Assert.IsType<NotFoundResult>(result);

        // Confirm the alert is genuinely untouched in Tenant A's view.
        db.Tenant.TenantId = TenantA;
        var reloaded = await db.Context.Alerts.FirstAsync(a => a.Id == alert.Id);
        Assert.Null(reloaded.ReadAt);
    }

    [Fact]
    public async Task AlertsController_Dismiss_returns_NotFound_for_other_tenants_alert()
    {
        using var db = NewDbWithTwoTenants();

        db.Tenant.TenantId = TenantA;
        var alert = new Alert
        {
            TenantId = TenantA,
            Type = AlertType.KiddingUpcoming,
            Severity = AlertSeverity.Info,
            Title = "Kidding",
            Body = "...",
        };
        db.Context.Alerts.Add(alert);
        await db.Context.SaveChangesAsync();

        db.Tenant.TenantId = TenantB;
        var ctrl = new AlertsController(db.Context);
        var result = await ctrl.Dismiss(alert.Id);

        Assert.IsType<NotFoundResult>(result);

        db.Tenant.TenantId = TenantA;
        var reloaded = await db.Context.Alerts.FirstAsync(a => a.Id == alert.Id);
        Assert.Null(reloaded.DismissedAt);
    }

    [Fact]
    public async Task BarnsController_Delete_returns_NotFound_for_other_tenants_barn()
    {
        using var db = NewDbWithTwoTenants();

        db.Tenant.TenantId = TenantA;
        var barn = new Barn { TenantId = TenantA, Name = "Big Red", Description = "" };
        db.Context.Barns.Add(barn);
        await db.Context.SaveChangesAsync();

        db.Tenant.TenantId = TenantB;
        var ctrl = new BarnsController(db.Context);
        var result = await ctrl.Delete(barn.Id);

        Assert.IsType<NotFoundResult>(result);

        // Bypass to confirm the barn still exists.
        db.Tenant.BypassFilter = true;
        var stillThere = await db.Context.Barns.AnyAsync(b => b.Id == barn.Id);
        Assert.True(stillThere);
    }

    [Fact]
    public async Task BarnsController_Update_does_not_mutate_other_tenants_barn()
    {
        using var db = NewDbWithTwoTenants();

        db.Tenant.TenantId = TenantA;
        var barn = new Barn { TenantId = TenantA, Name = "Original", Description = "" };
        db.Context.Barns.Add(barn);
        await db.Context.SaveChangesAsync();

        db.Tenant.TenantId = TenantB;
        var ctrl = new BarnsController(db.Context);
        // The update DTO carries Tenant A's barn ID + Tenant B's chosen new name.
        var result = await ctrl.Update(barn.Id,
            new Barn { Id = barn.Id, Name = "Hacked", Description = "" });

        Assert.IsType<NotFoundResult>(result);

        db.Tenant.BypassFilter = true;
        var reloaded = await db.Context.Barns.FirstAsync(b => b.Id == barn.Id);
        Assert.Equal("Original", reloaded.Name);
    }

    [Fact]
    public async Task GoatsController_via_FirstOrDefault_filters_by_tenant()
    {
        // Doesn't go through GoatsController (heavy ctor deps); instead asserts
        // the underlying EF behavior the fix relies on: FirstOrDefaultAsync
        // applies the global tenant filter, FindAsync doesn't.
        using var db = NewDbWithTwoTenants();

        db.Tenant.TenantId = TenantA;
        var goat = new Goat { TenantId = TenantA, Name = "Daisy", Gender = Gender.Female };
        db.Context.Goats.Add(goat);
        await db.Context.SaveChangesAsync();

        db.Tenant.TenantId = TenantB;

        // Sanity: FindAsync would still return the cached entity (it's tracked).
        // The IDOR safety relies on FirstOrDefaultAsync(g => g.Id == id) which
        // does respect the filter — that's what the controllers now use.
        var leaked = await db.Context.Goats.FirstOrDefaultAsync(g => g.Id == goat.Id);
        Assert.Null(leaked);
    }
}
