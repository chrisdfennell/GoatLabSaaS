using GoatLab.Server.Services.Pdf;
using GoatLab.Shared.Models;

namespace GoatLab.Tests;

// PDF templates need to render without throwing for the realistic data shapes
// we hand them. We don't visually inspect output here — just confirm the byte
// stream is a real PDF (starts with %PDF) and is non-trivial in size.
public class PdfServiceTests
{
    static PdfServiceTests()
    {
        // Match the Program.cs license setup so QuestPDF doesn't refuse to render.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static bool IsPdf(byte[] bytes) =>
        bytes.Length > 100 &&
        bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46;

    [Fact]
    public async Task Pedigree_renders_for_goat_with_full_pedigree()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant { Id = 1, Name = "Acme", Slug = "acme", PlanId = 3 });
        db.Context.SaveChanges();
        db.Tenant.TenantId = 1;

        var grandsire = new Goat { Name = "GrandSir", Gender = Gender.Male, TenantId = 1 };
        var granddam = new Goat { Name = "GrandDam", Gender = Gender.Female, TenantId = 1 };
        db.Context.Goats.AddRange(grandsire, granddam);
        db.Context.SaveChanges();

        var sire = new Goat { Name = "Sir", Gender = Gender.Male, TenantId = 1, SireId = grandsire.Id, DamId = granddam.Id };
        var dam = new Goat { Name = "Dam", Gender = Gender.Female, TenantId = 1 };
        db.Context.Goats.AddRange(sire, dam);
        db.Context.SaveChanges();

        var goat = new Goat { Name = "Subject", Gender = Gender.Female, TenantId = 1, SireId = sire.Id, DamId = dam.Id, DateOfBirth = DateTime.UtcNow.AddYears(-2) };
        db.Context.Goats.Add(goat);
        db.Context.SaveChanges();

        var svc = new PdfService(db.Context);
        var bytes = await svc.GeneratePedigreeAsync(goat.Id, "Acme Farms");

        Assert.NotNull(bytes);
        Assert.True(IsPdf(bytes!), "Output is not a PDF.");
    }

    [Fact]
    public async Task Pedigree_returns_null_for_unknown_goat()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Tenant.TenantId = 1;
        var svc = new PdfService(db.Context);
        Assert.Null(await svc.GeneratePedigreeAsync(99999, "Acme"));
    }

    [Fact]
    public async Task Sales_contract_renders_with_customer_and_goat()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant { Id = 1, Name = "Acme", Slug = "acme", PlanId = 3 });
        db.Context.SaveChanges();
        db.Tenant.TenantId = 1;

        var customer = new Customer { Name = "Jane Buyer", Email = "j@b.com", TenantId = 1 };
        var goat = new Goat { Name = "Pickle", TenantId = 1 };
        db.Context.AddRange(customer, goat);
        db.Context.SaveChanges();

        var sale = new Sale
        {
            TenantId = 1, CustomerId = customer.Id, GoatId = goat.Id,
            SaleType = SaleType.LiveAnimal, SaleDate = DateTime.UtcNow,
            Amount = 500, DepositAmount = 100, PaymentStatus = PaymentStatus.Deposited,
            Description = "Doeling reservation",
        };
        db.Context.Sales.Add(sale);
        db.Context.SaveChanges();

        var svc = new PdfService(db.Context);
        var bytes = await svc.GenerateSalesContractAsync(sale.Id, "Acme Farms");

        Assert.NotNull(bytes);
        Assert.True(IsPdf(bytes!));
    }

    [Fact]
    public async Task Health_certificate_renders_with_no_records()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant { Id = 1, Name = "Acme", Slug = "acme", PlanId = 3 });
        db.Context.SaveChanges();
        db.Tenant.TenantId = 1;

        var goat = new Goat { Name = "NoData", TenantId = 1 };
        db.Context.Goats.Add(goat);
        db.Context.SaveChanges();

        var svc = new PdfService(db.Context);
        var bytes = await svc.GenerateHealthCertificateAsync(goat.Id, "Acme Farms");

        Assert.NotNull(bytes);
        Assert.True(IsPdf(bytes!));
    }
}
