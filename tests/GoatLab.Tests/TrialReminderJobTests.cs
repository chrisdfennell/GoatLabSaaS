using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services.Jobs;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoatLab.Tests;

// TrialReminderJob hits a handful of tricky edges: time windows, tenants
// without owners, already-sent reminders, and per-subscription idempotency.
public class TrialReminderJobTests
{
    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().Build();

    private static async Task<(TestDb db, CapturingEmailSender email)> SetupAsync(
        DateTime? trialEnds, DateTime? reminderSentAt = null)
    {
        var db = new TestDb();
        db.SeedDefaultPlans();
        var tenant = new Tenant
        {
            Id = 1, Name = "Acme", Slug = "acme", PlanId = 2, // Farm
            TrialEndsAt = trialEnds,
            TrialReminderSentAt = reminderSentAt,
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

        return (db, new CapturingEmailSender());
    }

    [Fact]
    public async Task Sends_email_when_trial_ends_within_3_days()
    {
        var (db, email) = await SetupAsync(trialEnds: DateTime.UtcNow.AddDays(2));
        try
        {
            var job = new TrialReminderJob(db.Context, db.BuildUserManager(), email,
                NullLogger<TrialReminderJob>.Instance, EmptyConfig());

            await job.RunAsync(CancellationToken.None);

            Assert.Single(email.Sent);
            Assert.Equal("owner@example.com", email.Sent[0].To);
            Assert.Contains("trial", email.Sent[0].Subject, StringComparison.OrdinalIgnoreCase);

            var tenant = await db.Context.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == 1);
            Assert.NotNull(tenant.TrialReminderSentAt);
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Skips_when_trial_ends_beyond_3_days()
    {
        var (db, email) = await SetupAsync(trialEnds: DateTime.UtcNow.AddDays(10));
        try
        {
            var job = new TrialReminderJob(db.Context, db.BuildUserManager(), email,
                NullLogger<TrialReminderJob>.Instance, EmptyConfig());

            await job.RunAsync(CancellationToken.None);

            Assert.Empty(email.Sent);
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Skips_when_trial_already_expired()
    {
        var (db, email) = await SetupAsync(trialEnds: DateTime.UtcNow.AddDays(-1));
        try
        {
            var job = new TrialReminderJob(db.Context, db.BuildUserManager(), email,
                NullLogger<TrialReminderJob>.Instance, EmptyConfig());

            await job.RunAsync(CancellationToken.None);

            Assert.Empty(email.Sent);
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Idempotent_does_not_resend_when_already_stamped()
    {
        var (db, email) = await SetupAsync(
            trialEnds: DateTime.UtcNow.AddDays(2),
            reminderSentAt: DateTime.UtcNow.AddHours(-1));
        try
        {
            var job = new TrialReminderJob(db.Context, db.BuildUserManager(), email,
                NullLogger<TrialReminderJob>.Instance, EmptyConfig());

            await job.RunAsync(CancellationToken.None);

            Assert.Empty(email.Sent);
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Skips_soft_deleted_tenant()
    {
        var (db, email) = await SetupAsync(trialEnds: DateTime.UtcNow.AddDays(2));
        try
        {
            db.Tenant.BypassFilter = true;
            var tenant = await db.Context.Tenants.FirstAsync(t => t.Id == 1);
            tenant.DeletedAt = DateTime.UtcNow.AddDays(-1);
            await db.Context.SaveChangesAsync();
            db.Tenant.BypassFilter = false;

            var job = new TrialReminderJob(db.Context, db.BuildUserManager(), email,
                NullLogger<TrialReminderJob>.Instance, EmptyConfig());

            await job.RunAsync(CancellationToken.None);

            Assert.Empty(email.Sent);
        }
        finally { db.Dispose(); }
    }
}
