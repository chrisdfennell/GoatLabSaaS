using System.Net;
using System.Text;
using GoatLab.Server.Services.Webhooks;
using GoatLab.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoatLab.Tests;

public class WebhookDispatcherTests
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

    private static Webhook AddWebhook(TestDb db, string events, bool active = true, string url = "https://example.test/hook")
    {
        var w = new Webhook
        {
            TenantId = TenantId,
            Name = "Test",
            Url = url,
            Secret = "topsecret",
            Events = events,
            IsActive = active,
        };
        db.Context.Webhooks.Add(w);
        db.Context.SaveChanges();
        return w;
    }

    [Fact]
    public void ComputeSignature_matches_hmac_sha256_of_payload()
    {
        // HMAC-SHA256("topsecret", "hello") — precomputed.
        var sig = WebhookDispatcher.ComputeSignature("hello", "topsecret");
        Assert.Equal(64, sig.Length);
        Assert.Matches("^[0-9a-f]{64}$", sig);

        // Different payload → different signature.
        var other = WebhookDispatcher.ComputeSignature("world", "topsecret");
        Assert.NotEqual(sig, other);
    }

    [Fact]
    public async Task DispatchAsync_sends_signed_post_to_subscribed_webhooks_only()
    {
        using var db = NewDb();
        var subscribed = AddWebhook(db, "goat.created,goat.updated", url: "https://sub.test/hook");
        var other = AddWebhook(db, "sale.created", url: "https://other.test/hook");

        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var http = new StubHttpClientFactory(async (req, ct) =>
        {
            captured = req;
            capturedBody = await req.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok"),
            };
        });

        var dispatcher = new WebhookDispatcher(db.Context, http, NullLogger<WebhookDispatcher>.Instance);
        await dispatcher.DispatchAsync("goat.created", new { id = 7, name = "Clover" });

        Assert.NotNull(captured);
        Assert.Equal("https://sub.test/hook", captured!.RequestUri!.ToString());
        Assert.Equal("goat.created", captured.Headers.GetValues("X-GoatLab-Event").Single());
        Assert.Single(captured.Headers.GetValues("X-GoatLab-Delivery"));
        var sigHeader = captured.Headers.GetValues("X-GoatLab-Signature").Single();
        Assert.StartsWith("sha256=", sigHeader);

        Assert.NotNull(capturedBody);
        Assert.Contains("goat.created", capturedBody!);
        Assert.Equal($"sha256={WebhookDispatcher.ComputeSignature(capturedBody!, subscribed.Secret)}", sigHeader);

        // Exactly one delivery row (other webhook not subscribed).
        var deliveries = db.Context.WebhookDeliveries.ToList();
        var d = Assert.Single(deliveries);
        Assert.Equal(subscribed.Id, d.WebhookId);
        Assert.Equal(200, d.StatusCode);
        Assert.NotNull(d.DeliveredAt);
        Assert.Null(d.NextRetryAt);
    }

    [Fact]
    public async Task DispatchAsync_schedules_retry_when_receiver_returns_500()
    {
        using var db = NewDb();
        var webhook = AddWebhook(db, "goat.created");
        var http = new StubHttpClientFactory((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("boom"),
            }));

        var dispatcher = new WebhookDispatcher(db.Context, http, NullLogger<WebhookDispatcher>.Instance);
        await dispatcher.DispatchAsync("goat.created", new { id = 1 });

        var delivery = Assert.Single(db.Context.WebhookDeliveries);
        Assert.Equal(500, delivery.StatusCode);
        Assert.Null(delivery.DeliveredAt);
        Assert.NotNull(delivery.NextRetryAt);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Equal("HTTP 500", delivery.Error);
    }

    [Fact]
    public async Task DispatchAsync_skips_inactive_webhooks()
    {
        using var db = NewDb();
        AddWebhook(db, "goat.created", active: false);
        var http = new StubHttpClientFactory((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var dispatcher = new WebhookDispatcher(db.Context, http, NullLogger<WebhookDispatcher>.Instance);
        await dispatcher.DispatchAsync("goat.created", new { id = 1 });

        Assert.Empty(db.Context.WebhookDeliveries);
    }
}

// Minimal IHttpClientFactory for tests — routes every Create() call through a
// stub handler so we can observe outgoing webhook traffic.
internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;
    public StubHttpClientFactory(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name) => new HttpClient(new DelegateHandler(_handler));

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _fn;
        public DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> fn) { _fn = fn; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => _fn(request, ct);
    }
}
