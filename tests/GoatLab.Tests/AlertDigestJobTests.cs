using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services.Jobs;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoatLab.Tests;

// AlertDigestJob — pin down: respects AlertEmailEnabled, only includes the
// last 24h, skips tenants whose plan doesn't have SmartAlerts, no-op when no
// alerts in the window, and sends to the first Owner.
public class AlertDigestJobTests
{
    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();

    private static async Task<(TestDb db, CapturingEmailSender email, Tenant tenant)> SetupAsync(
        bool alertEmailEnabled = true, int planId = 3 /* Dairy */)
    {
        var db = new TestDb();
        db.SeedDefaultPlans();
        var tenant = new Tenant
        {
            Id = 1, Name = "Acme", Slug = "acme", PlanId = planId,
            AlertEmailEnabled = alertEmailEnabled,
        };
        db.Context.Tenants.Add(tenant);

        var user = new ApplicationUser
        {
            Id = "u-1",
            UserName = "owner@example.com",
            NormalizedUserName = "OWNER@EXAMPLE.COM",
            Email = "owner@example.com",
            NormalizedEmail = "OWNER@EXAMPLE.COM",
            DisplayName = "Owner",
            EmailConfirmed = true,
        };
        db.Context.Users.Add(user);
        db.Context.TenantMembers.Add(new TenantMember
        {
            Id = 1, TenantId = 1, UserId = user.Id, Role = TenantRole.Owner,
        });
        await db.Context.SaveChangesAsync();
        return (db, new CapturingEmailSender(), tenant);
    }

    private static async Task RunAsync(TestDb db, CapturingEmailSender email)
    {
        var job = new AlertDigestJob(db.Context, db.BuildUserManager(), email,
            NullLogger<AlertDigestJob>.Instance, EmptyConfig());
        await job.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Sends_one_email_with_alerts_from_last_24h()
    {
        var (db, email, _) = await SetupAsync();
        try
        {
            db.Context.Alerts.AddRange(
                new Alert { TenantId = 1, Type = AlertType.MedicationOverdue, Severity = AlertSeverity.Error,
                    Title = "Overdue: CDT booster", Body = "Daisy", CreatedAt = DateTime.UtcNow.AddHours(-2) },
                new Alert { TenantId = 1, Type = AlertType.LowFeedStock, Severity = AlertSeverity.Warning,
                    Title = "Low feed", Body = "Alfalfa pellets", CreatedAt = DateTime.UtcNow.AddHours(-12) });
            await db.Context.SaveChangesAsync();

            await RunAsync(db, email);

            Assert.Single(email.Sent);
            Assert.Equal("owner@example.com", email.Sent[0].To);
            Assert.Contains("2 new alerts", email.Sent[0].Subject);
            Assert.Contains("CDT booster", email.Sent[0].Html);
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Skips_alerts_older_than_24h()
    {
        var (db, email, _) = await SetupAsync();
        try
        {
            db.Context.Alerts.Add(new Alert
            {
                TenantId = 1, Type = AlertType.MedicationOverdue, Severity = AlertSeverity.Error,
                Title = "Old alert", CreatedAt = DateTime.UtcNow.AddHours(-30),
            });
            await db.Context.SaveChangesAsync();

            await RunAsync(db, email);

            Assert.Empty(email.Sent);
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Skips_when_AlertEmailEnabled_is_false()
    {
        var (db, email, _) = await SetupAsync(alertEmailEnabled: false);
        try
        {
            db.Context.Alerts.Add(new Alert
            {
                TenantId = 1, Type = AlertType.MedicationOverdue, Severity = AlertSeverity.Error,
                Title = "Whatever", CreatedAt = DateTime.UtcNow.AddHours(-1),
            });
            await db.Context.SaveChangesAsync();

            await RunAsync(db, email);

            Assert.Empty(email.Sent);
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Skips_when_plan_lacks_SmartAlerts()
    {
        // Build a custom Homestead-like plan WITHOUT SmartAlerts toggled on.
        var (db, email, _) = await SetupAsync(planId: 1 /* Homestead from SeedDefaultPlans, which doesn't include new keys */);
        try
        {
            db.Context.Alerts.Add(new Alert
            {
                TenantId = 1, Type = AlertType.MedicationOverdue, Severity = AlertSeverity.Error,
                Title = "X", CreatedAt = DateTime.UtcNow.AddHours(-1),
            });
            await db.Context.SaveChangesAsync();

            await RunAsync(db, email);

            Assert.Empty(email.Sent);
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Skips_when_no_alerts_in_window()
    {
        var (db, email, _) = await SetupAsync();
        try
        {
            await RunAsync(db, email);
            Assert.Empty(email.Sent);
        }
        finally { db.Dispose(); }
    }
}
