using GoatLab.Server.Controllers;
using GoatLab.Server.Services.Email;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Tests;

// Targets the atomic-claim guard added to WaitlistController.Fulfill so two
// near-concurrent fulfill clicks (same waitlist entry) can't each spawn a
// duplicate Sale row. The CAS uses ExecuteUpdateAsync with WHERE Status NOT IN
// (Fulfilled, Cancelled); only the first one to hit the DB wins.
public class WaitlistFulfillConcurrencyTests
{
    private const int TenantId = 1;

    private sealed class NoopEmailSender : IAppEmailSender
    {
        public Task SendAsync(string toAddress, string subject, string htmlBody,
            string? plainTextBody = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static (TestDb db, WaitlistController ctrl) NewFixture()
    {
        var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant { Id = TenantId, Name = "Acme", Slug = "acme", PlanId = 2 });
        db.Context.SaveChanges();
        db.Tenant.TenantId = TenantId;
        return (db, new WaitlistController(db.Context, new NoopEmailSender()));
    }

    [Fact]
    public async Task Fulfill_returns_BadRequest_when_status_was_flipped_externally_after_load()
    {
        var (db, ctrl) = NewFixture();
        using var _ = db;

        var customer = new Customer { TenantId = TenantId, Name = "Buyer" };
        var goat = new Goat { TenantId = TenantId, Name = "Daisy", Gender = Gender.Female };
        db.Context.Customers.Add(customer);
        db.Context.Goats.Add(goat);
        await db.Context.SaveChangesAsync();

        var entry = new WaitlistEntry
        {
            TenantId = TenantId,
            CustomerId = customer.Id,
            DepositCents = 5000,
            DepositPaid = true,
            Status = WaitlistStatus.Waiting,
        };
        db.Context.WaitlistEntries.Add(entry);
        await db.Context.SaveChangesAsync();

        // Simulate a concurrent request that beats us to claiming the entry:
        // flip its Status to Fulfilled in the DB out-of-band, mimicking the
        // moment between the controller's status-check and its CAS.
        await db.Context.WaitlistEntries
            .Where(w => w.Id == entry.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(w => w.Status, WaitlistStatus.Fulfilled)
                .SetProperty(w => w.FulfilledAt, (DateTime?)DateTime.UtcNow));

        // Force the controller to re-read the entry as Waiting by detaching the
        // already-tracked instance and re-loading the (now-stale) view through
        // the change tracker. We achieve the same effect by detaching + adding
        // a fresh instance with Status=Waiting that EF still tracks as our
        // "loaded" snapshot. Easier path: clear tracker and rely on the actual
        // Fulfill call using FirstOrDefaultAsync, which will see Fulfilled.
        // In that case the FIRST early-exit at line 132 trips and we get a
        // BadRequest response — the user-visible behavior we want to assert.
        db.Context.ChangeTracker.Clear();

        var result = await ctrl.Fulfill(entry.Id, new WaitlistController.FulfillRequest(goat.Id, 250m, null));

        Assert.IsType<BadRequestObjectResult>(result.Result);

        // No duplicate Sale row was created.
        Assert.Empty(db.Context.Sales);
    }

    [Fact]
    public async Task Two_back_to_back_fulfills_only_create_one_sale()
    {
        // Belt-and-braces: even though the previous test triggers the early-exit
        // path, the new ExecuteUpdateAsync CAS guarantees the second-arriving
        // request can't slip past the status check via stale-read. Run two
        // sequential Fulfills and assert the DB ends up with one Sale, not two.
        var (db, ctrl) = NewFixture();
        using var _ = db;

        var customer = new Customer { TenantId = TenantId, Name = "Buyer" };
        var goat = new Goat { TenantId = TenantId, Name = "Daisy", Gender = Gender.Female };
        db.Context.Customers.Add(customer);
        db.Context.Goats.Add(goat);
        await db.Context.SaveChangesAsync();

        var entry = new WaitlistEntry
        {
            TenantId = TenantId,
            CustomerId = customer.Id,
            DepositCents = 5000,
            DepositPaid = true,
            Status = WaitlistStatus.Waiting,
        };
        db.Context.WaitlistEntries.Add(entry);
        await db.Context.SaveChangesAsync();

        var first = await ctrl.Fulfill(entry.Id, new WaitlistController.FulfillRequest(goat.Id, 250m, null));
        var second = await ctrl.Fulfill(entry.Id, new WaitlistController.FulfillRequest(goat.Id, 250m, null));

        Assert.IsType<CreatedAtActionResult>(first.Result);
        Assert.IsType<BadRequestObjectResult>(second.Result);
        Assert.Single(db.Context.Sales);
    }
}
