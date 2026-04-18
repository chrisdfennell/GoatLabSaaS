using GoatLab.Server.Controllers;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GoatLab.Tests;

// Exercises WaitlistController directly (no HTTP pipeline). Covers the
// create → offer → fulfill happy path, plus guards on finalised entries.
public class WaitlistTests
{
    private const int TenantId = 1;

    private static (TestDb db, WaitlistController ctrl) NewFixture()
    {
        var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant { Id = TenantId, Name = "Acme", Slug = "acme", PlanId = 2 });
        db.Context.SaveChanges();
        db.Tenant.TenantId = TenantId;
        return (db, new WaitlistController(db.Context));
    }

    private static int AddCustomer(TestDb db, string name = "Buyer")
    {
        var c = new Customer { TenantId = TenantId, Name = name };
        db.Context.Customers.Add(c);
        db.Context.SaveChanges();
        return c.Id;
    }

    private static int AddGoat(TestDb db, string name = "Kid")
    {
        var g = new Goat { TenantId = TenantId, Name = name, Gender = Gender.Female };
        db.Context.Goats.Add(g);
        db.Context.SaveChanges();
        return g.Id;
    }

    [Fact]
    public async Task Create_adds_entry_with_sanitised_defaults()
    {
        var (db, ctrl) = NewFixture();
        using var _ = db;
        var customerId = AddCustomer(db);

        // Even if the client submits a Fulfilled status, the server forces Waiting.
        var input = new WaitlistEntry
        {
            CustomerId = customerId,
            Status = WaitlistStatus.Fulfilled,
            DepositCents = 5000,
            DepositPaid = true,
            Priority = 3,
        };

        var result = await ctrl.Create(input);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var entry = Assert.IsType<WaitlistEntry>(created.Value);

        Assert.Equal(WaitlistStatus.Waiting, entry.Status);
        Assert.NotNull(entry.DepositReceivedAt);
        Assert.Equal(3, entry.Priority);
    }

    [Fact]
    public async Task Fulfill_creates_linked_sale_and_marks_entry_fulfilled()
    {
        var (db, ctrl) = NewFixture();
        using var _ = db;
        var customerId = AddCustomer(db);
        var goatId = AddGoat(db);

        var entry = new WaitlistEntry
        {
            CustomerId = customerId,
            DepositCents = 10000, // $100
            DepositPaid = true,
        };
        await ctrl.Create(entry);
        // EF assigned the Id after Create.
        var entryId = db.Context.WaitlistEntries.Single().Id;

        var fulfillResult = await ctrl.Fulfill(entryId, new WaitlistController.FulfillRequest(goatId, 500m, null));
        var created = Assert.IsType<CreatedAtActionResult>(fulfillResult.Result);
        var sale = Assert.IsType<Sale>(created.Value);

        Assert.Equal(SaleType.LiveAnimal, sale.SaleType);
        Assert.Equal(500m, sale.Amount);
        Assert.Equal(100m, sale.DepositAmount);
        Assert.Equal(PaymentStatus.Deposited, sale.PaymentStatus);
        Assert.Equal(goatId, sale.GoatId);

        var reloaded = db.Context.WaitlistEntries.Single();
        Assert.Equal(WaitlistStatus.Fulfilled, reloaded.Status);
        Assert.Equal(sale.Id, reloaded.FulfilledSaleId);
        Assert.Equal(goatId, reloaded.FulfilledGoatId);

        // Finance mirror should have received the deposit as income.
        var tx = Assert.Single(db.Context.Transactions);
        Assert.Equal(TransactionType.Income, tx.Type);
        Assert.Equal(100m, tx.Amount);
        Assert.Equal(sale.Id, tx.SaleId);
    }

    [Fact]
    public async Task Fulfill_rejects_already_fulfilled_entry()
    {
        var (db, ctrl) = NewFixture();
        using var _ = db;
        var customerId = AddCustomer(db);
        var goatId = AddGoat(db);

        var entry = new WaitlistEntry { CustomerId = customerId, DepositCents = 0 };
        await ctrl.Create(entry);
        var id = db.Context.WaitlistEntries.Single().Id;

        var first = await ctrl.Fulfill(id, new WaitlistController.FulfillRequest(goatId, 300m, null));
        Assert.IsType<CreatedAtActionResult>(first.Result);

        var second = await ctrl.Fulfill(id, new WaitlistController.FulfillRequest(goatId, 300m, null));
        Assert.IsType<BadRequestObjectResult>(second.Result);
    }

    [Fact]
    public async Task Cancel_sets_status_and_blocks_further_transitions()
    {
        var (db, ctrl) = NewFixture();
        using var _ = db;
        var customerId = AddCustomer(db);
        var goatId = AddGoat(db);

        var entry = new WaitlistEntry { CustomerId = customerId };
        await ctrl.Create(entry);
        var id = db.Context.WaitlistEntries.Single().Id;

        var cancel = await ctrl.Cancel(id, new WaitlistController.CancelRequest("changed mind"));
        Assert.IsType<NoContentResult>(cancel);

        // Offer now blocked.
        var offer = await ctrl.Offer(id);
        Assert.IsType<BadRequestObjectResult>(offer);

        // Fulfill also blocked.
        var fulfill = await ctrl.Fulfill(id, new WaitlistController.FulfillRequest(goatId, 100m, null));
        Assert.IsType<BadRequestObjectResult>(fulfill.Result);
    }
}
