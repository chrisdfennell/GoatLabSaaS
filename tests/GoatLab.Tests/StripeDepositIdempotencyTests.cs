using System.Security.Cryptography;
using System.Text;
using GoatLab.Server.Services.Billing;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoatLab.Tests;

// Replays of `checkout.session.completed` for a public-listing deposit must
// produce exactly one WaitlistEntry. The fix swapped a substring-matching
// idempotency check (Notes.Contains(session.Id)) for a dedicated indexed
// column StripeCheckoutSessionId, which we now exercise here.
public class StripeDepositIdempotencyTests
{
    private const string Secret = "whsec_test_deadbeef";
    private const int TenantId = 1;
    private const int GoatId = 100;
    private const string SessionId = "cs_test_deposit_abc123";

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

    // Synthesises a Stripe checkout.session.completed event with the metadata
    // shape produced by CreateDepositCheckoutSessionAsync.
    private static (string Payload, string Signature) DepositSessionCompleted(
        int tenantId, int goatId, string sessionId, string buyerEmail, int amountCents)
    {
        var payload = $@"{{
          ""id"": ""evt_deposit_{sessionId}"",
          ""object"": ""event"",
          ""api_version"": ""2026-03-25.dahlia"",
          ""type"": ""checkout.session.completed"",
          ""data"": {{
            ""object"": {{
              ""id"": ""{sessionId}"",
              ""object"": ""checkout.session"",
              ""amount_total"": {amountCents},
              ""customer_email"": ""{buyerEmail}"",
              ""metadata"": {{
                ""tenant_id"": ""{tenantId}"",
                ""deposit_goat_id"": ""{goatId}"",
                ""deposit_buyer_email"": ""{buyerEmail}"",
                ""deposit_buyer_name"": ""Test Buyer"",
                ""deposit_buyer_phone"": """",
                ""deposit_notes"": """"
              }}
            }}
          }}
        }}";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return (payload, BuildSignatureHeader(payload, ts, Secret));
    }

    private static async Task<TestDb> SeedAsync()
    {
        var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Cedar Farm",
            Slug = "cedar",
            PlanId = 2,
            PublicProfileEnabled = true,
        });
        db.Context.Goats.Add(new Goat
        {
            Id = GoatId,
            TenantId = TenantId,
            Name = "Daisy",
            Gender = Gender.Female,
            IsListedForSale = true,
        });
        await db.Context.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task First_deposit_event_creates_one_customer_and_one_waitlist_entry()
    {
        using var db = await SeedAsync();
        var svc = new StripeBillingService(Options.Create(Opts()), db.Context, db.Tenant,
            NullLogger<StripeBillingService>.Instance);

        var (payload, sig) = DepositSessionCompleted(TenantId, GoatId, SessionId, "buyer@example.com", 5000);

        await svc.HandleWebhookAsync(payload, sig, CancellationToken.None);

        db.Tenant.BypassFilter = true;
        var entry = Assert.Single(db.Context.WaitlistEntries);
        Assert.Equal(SessionId, entry.StripeCheckoutSessionId);
        Assert.Equal(5000, entry.DepositCents);
        Assert.True(entry.DepositPaid);
        Assert.Single(db.Context.Customers);
    }

    [Fact]
    public async Task Replay_of_same_session_id_does_not_create_duplicates()
    {
        using var db = await SeedAsync();
        var svc = new StripeBillingService(Options.Create(Opts()), db.Context, db.Tenant,
            NullLogger<StripeBillingService>.Instance);

        var (payload, sig) = DepositSessionCompleted(TenantId, GoatId, SessionId, "buyer@example.com", 5000);

        // Stripe occasionally retries — our idempotency key is the dedicated
        // StripeCheckoutSessionId column on WaitlistEntry. The second call must
        // be a no-op, not a duplicate row.
        await svc.HandleWebhookAsync(payload, sig, CancellationToken.None);
        // Build a fresh signed payload so the signature is still valid; the
        // session.id is what matters for idempotency, not the event timestamp.
        var (payload2, sig2) = DepositSessionCompleted(TenantId, GoatId, SessionId, "buyer@example.com", 5000);
        await svc.HandleWebhookAsync(payload2, sig2, CancellationToken.None);

        db.Tenant.BypassFilter = true;
        Assert.Single(db.Context.WaitlistEntries);
        Assert.Single(db.Context.Customers);
    }

    [Fact]
    public async Task Distinct_session_ids_create_distinct_entries()
    {
        using var db = await SeedAsync();
        var svc = new StripeBillingService(Options.Create(Opts()), db.Context, db.Tenant,
            NullLogger<StripeBillingService>.Instance);

        var (p1, s1) = DepositSessionCompleted(TenantId, GoatId, "cs_test_one", "a@example.com", 5000);
        var (p2, s2) = DepositSessionCompleted(TenantId, GoatId, "cs_test_two", "b@example.com", 7500);

        await svc.HandleWebhookAsync(p1, s1, CancellationToken.None);
        await svc.HandleWebhookAsync(p2, s2, CancellationToken.None);

        db.Tenant.BypassFilter = true;
        var entries = await db.Context.WaitlistEntries
            .OrderBy(w => w.StripeCheckoutSessionId)
            .ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Equal("cs_test_one", entries[0].StripeCheckoutSessionId);
        Assert.Equal("cs_test_two", entries[1].StripeCheckoutSessionId);
    }
}
