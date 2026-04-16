using System.Security.Cryptography;
using System.Text;
using GoatLab.Server.Services.Billing;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;

namespace GoatLab.Tests;

// Stripe webhook handler is the most fragile thing in the app — one slip and
// we under/over bill. Tests cover: signature check rejects tampering, a valid
// subscription.updated applies all expected fields, subscription.deleted
// reverts to the free plan.
public class StripeWebhookTests
{
    private const string Secret = "whsec_test_deadbeef";

    private static StripeOptions Opts() => new()
    {
        SecretKey = "sk_test_x",
        PublishableKey = "pk_test_x",
        WebhookSecret = Secret,
    };

    private static string BuildSignatureHeader(string payload, long timestamp, string secret)
    {
        var signed = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hex = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signed))).ToLowerInvariant();
        return $"t={timestamp},v1={hex}";
    }

    private static (string Payload, string Signature) SubscriptionUpdatedPayload(int tenantId, int planId,
        string status, long trialEnd, long periodEnd)
    {
        var payload = $@"{{
          ""id"": ""evt_test"",
          ""object"": ""event"",
          ""api_version"": ""2026-03-25.dahlia"",
          ""type"": ""customer.subscription.updated"",
          ""data"": {{
            ""object"": {{
              ""id"": ""sub_test"",
              ""object"": ""subscription"",
              ""status"": ""{status}"",
              ""trial_end"": {trialEnd},
              ""items"": {{
                ""object"": ""list"",
                ""data"": [{{
                  ""id"": ""si_test"",
                  ""object"": ""subscription_item"",
                  ""current_period_end"": {periodEnd}
                }}]
              }},
              ""metadata"": {{
                ""tenant_id"": ""{tenantId}"",
                ""plan_id"": ""{planId}""
              }}
            }}
          }}
        }}";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return (payload, BuildSignatureHeader(payload, ts, Secret));
    }

    private static (string Payload, string Signature) SubscriptionDeletedPayload(int tenantId)
    {
        var payload = $@"{{
          ""id"": ""evt_deleted"",
          ""object"": ""event"",
          ""api_version"": ""2026-03-25.dahlia"",
          ""type"": ""customer.subscription.deleted"",
          ""data"": {{
            ""object"": {{
              ""id"": ""sub_test"",
              ""object"": ""subscription"",
              ""status"": ""canceled"",
              ""metadata"": {{
                ""tenant_id"": ""{tenantId}""
              }}
            }}
          }}
        }}";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return (payload, BuildSignatureHeader(payload, ts, Secret));
    }

    [Fact]
    public async Task Invalid_signature_throws_StripeException()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        var svc = new StripeBillingService(Options.Create(Opts()), db.Context, db.Tenant,
            NullLogger<StripeBillingService>.Instance);

        var payload = @"{""id"":""evt_x"",""type"":""customer.subscription.updated"",""data"":{""object"":{""id"":""sub_x""}}}";
        var bogusSignature = "t=123,v1=00";

        await Assert.ThrowsAsync<StripeException>(() =>
            svc.HandleWebhookAsync(payload, bogusSignature, CancellationToken.None));
    }

    [Fact]
    public async Task Subscription_updated_applies_status_period_trial_and_plan()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant { Id = 1, Name = "Acme", Slug = "acme", PlanId = 1 });
        await db.Context.SaveChangesAsync();

        var trialEnd = DateTimeOffset.UtcNow.AddDays(14).ToUnixTimeSeconds();
        var periodEnd = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
        var (payload, sig) = SubscriptionUpdatedPayload(tenantId: 1, planId: 2, "trialing", trialEnd, periodEnd);

        var svc = new StripeBillingService(Options.Create(Opts()), db.Context, db.Tenant,
            NullLogger<StripeBillingService>.Instance);

        await svc.HandleWebhookAsync(payload, sig, CancellationToken.None);

        db.Tenant.BypassFilter = true;
        var tenant = await db.Context.Tenants.FirstAsync(t => t.Id == 1);
        Assert.Equal(2, tenant.PlanId); // upgraded to Farm via metadata
        Assert.Equal("sub_test", tenant.StripeSubscriptionId);
        Assert.Equal("trialing", tenant.SubscriptionStatus);
        Assert.NotNull(tenant.TrialEndsAt);
        Assert.NotNull(tenant.CurrentPeriodEnd);
    }

    [Fact]
    public async Task Subscription_updated_clears_trial_reminder_when_trial_end_changes()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant
        {
            Id = 1, Name = "Acme", Slug = "acme", PlanId = 2,
            TrialEndsAt = DateTime.UtcNow.AddDays(1),
            TrialReminderSentAt = DateTime.UtcNow.AddHours(-2),
        });
        await db.Context.SaveChangesAsync();

        // New trial end is 14 days out — different from stored.
        var newTrialEnd = DateTimeOffset.UtcNow.AddDays(14).ToUnixTimeSeconds();
        var periodEnd = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
        var (payload, sig) = SubscriptionUpdatedPayload(1, 2, "trialing", newTrialEnd, periodEnd);

        var svc = new StripeBillingService(Options.Create(Opts()), db.Context, db.Tenant,
            NullLogger<StripeBillingService>.Instance);
        await svc.HandleWebhookAsync(payload, sig, CancellationToken.None);

        db.Tenant.BypassFilter = true;
        var tenant = await db.Context.Tenants.FirstAsync(t => t.Id == 1);
        Assert.Null(tenant.TrialReminderSentAt); // cleared so the next run will resend
    }

    [Fact]
    public async Task Subscription_deleted_reverts_tenant_to_homestead_plan()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant
        {
            Id = 1, Name = "Acme", Slug = "acme", PlanId = 2, // Farm
            StripeSubscriptionId = "sub_test",
            SubscriptionStatus = "active",
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(10),
        });
        await db.Context.SaveChangesAsync();

        var (payload, sig) = SubscriptionDeletedPayload(1);
        var svc = new StripeBillingService(Options.Create(Opts()), db.Context, db.Tenant,
            NullLogger<StripeBillingService>.Instance);

        await svc.HandleWebhookAsync(payload, sig, CancellationToken.None);

        db.Tenant.BypassFilter = true;
        var tenant = await db.Context.Tenants.FirstAsync(t => t.Id == 1);
        Assert.Equal(1, tenant.PlanId); // reverted to Homestead (seeded Id=1)
        Assert.Equal("canceled", tenant.SubscriptionStatus);
        Assert.Null(tenant.StripeSubscriptionId);
        Assert.Null(tenant.CurrentPeriodEnd);
    }

    [Fact]
    public async Task Unrelated_event_is_a_noop()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant { Id = 1, Name = "Acme", Slug = "acme", PlanId = 1 });
        await db.Context.SaveChangesAsync();

        var payload = @"{""id"":""evt_ignored"",""api_version"":""2026-03-25.dahlia"",""type"":""product.updated"",""data"":{""object"":{}}}";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sig = BuildSignatureHeader(payload, ts, Secret);

        var svc = new StripeBillingService(Options.Create(Opts()), db.Context, db.Tenant,
            NullLogger<StripeBillingService>.Instance);

        // Should not throw; tenant untouched.
        await svc.HandleWebhookAsync(payload, sig, CancellationToken.None);

        db.Tenant.BypassFilter = true;
        var tenant = await db.Context.Tenants.FirstAsync(t => t.Id == 1);
        Assert.Equal(1, tenant.PlanId);
    }
}
