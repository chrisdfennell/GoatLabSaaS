using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoatLab.Tests;

// SQLite in-memory DbContext factory for tests. Connection stays open for the
// lifetime of the fixture — SQLite drops the DB the moment the last connection
// closes. EnsureCreated builds the schema from the EF model (we skip real
// migrations to dodge IDENTITY_INSERT / SQL Server-isms).
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _conn;
    public GoatLabDbContext Context { get; }
    public TestTenantContext Tenant { get; } = new();

    public TestDb()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        var options = new DbContextOptionsBuilder<GoatLabDbContext>()
            .UseSqlite(_conn)
            .Options;

        Context = new GoatLabDbContext(options, Tenant);
        Context.Database.EnsureCreated();
    }

    /// <summary>Seed the three default plans (features closely mirror production seeds).</summary>
    public void SeedDefaultPlans()
    {
        Context.Plans.AddRange(
            BuildPlan(1, "Homestead", "homestead", 0, 10, 1, 0, new[]
            {
                AppFeature.Goats, AppFeature.Health, AppFeature.Breeding, AppFeature.Milk,
                AppFeature.Calendar, AppFeature.Map, AppFeature.CareGuide, AppFeature.Barns,
            }),
            BuildPlan(2, "Farm", "farm", 1900, null, 3, 14, new[]
            {
                AppFeature.Goats, AppFeature.Health, AppFeature.Breeding, AppFeature.Milk,
                AppFeature.Sales, AppFeature.Finance, AppFeature.Inventory,
                AppFeature.Calendar, AppFeature.Map, AppFeature.CareGuide, AppFeature.Barns,
                AppFeature.DataExport,
            }),
            BuildPlan(3, "Dairy", "dairy", 4900, null, null, 0, Enum.GetValues<AppFeature>())
        );
        Context.SaveChanges();
    }

    private static Plan BuildPlan(int id, string name, string slug, int priceCents,
        int? maxGoats, int? maxUsers, int trialDays, IEnumerable<AppFeature> enabled)
    {
        return new Plan
        {
            Id = id,
            Name = name,
            Slug = slug,
            PriceMonthlyCents = priceCents,
            MaxGoats = maxGoats,
            MaxUsers = maxUsers,
            TrialDays = trialDays,
            IsPublic = true,
            IsActive = true,
            DisplayOrder = id,
            Features = enabled.Select(f => new PlanFeature { Feature = f, Enabled = true }).ToList(),
        };
    }

    /// <summary>Build a UserManager wired to the in-memory DB for job tests.</summary>
    public UserManager<ApplicationUser> BuildUserManager()
    {
        var store = new UserStore<ApplicationUser, IdentityRole, GoatLabDbContext>(Context);
        var identityOptions = Options.Create(new IdentityOptions());
        var hasher = new PasswordHasher<ApplicationUser>();
        var userValidators = new List<IUserValidator<ApplicationUser>> { new UserValidator<ApplicationUser>() };
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>> { new PasswordValidator<ApplicationUser>() };
        var normalizer = new UpperInvariantLookupNormalizer();
        var errors = new IdentityErrorDescriber();
        var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var logger = NullLogger<UserManager<ApplicationUser>>.Instance;
        return new UserManager<ApplicationUser>(
            store, identityOptions, hasher, userValidators, passwordValidators,
            normalizer, errors, serviceProvider, logger);
    }

    public void Dispose()
    {
        Context.Dispose();
        _conn.Dispose();
    }
}

/// <summary>IAppEmailSender that records every send call. Used by job tests.</summary>
public sealed class CapturingEmailSender : GoatLab.Server.Services.Email.IAppEmailSender
{
    public List<(string To, string Subject, string Html)> Sent { get; } = new();

    public Task SendAsync(string toAddress, string subject, string htmlBody,
        string? plainTextBody = null, CancellationToken cancellationToken = default)
    {
        Sent.Add((toAddress, subject, htmlBody));
        return Task.CompletedTask;
    }
}

public sealed class TestTenantContext : ITenantContext
{
    public int? TenantId { get; set; }
    public bool BypassFilter { get; set; }
}
