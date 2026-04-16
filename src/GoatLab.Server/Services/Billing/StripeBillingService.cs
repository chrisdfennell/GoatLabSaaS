using GoatLab.Server.Data;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

// Disambiguate Plan — we mean the app's subscription plan entity, not Stripe's
// legacy Plan object (which has been superseded by Price on Stripe's side anyway).
using Plan = GoatLab.Shared.Models.Plan;

namespace GoatLab.Server.Services.Billing;

public class StripeBillingService : IBillingService
{
    private const string TenantIdMetadataKey = "tenant_id";
    private const string PlanIdMetadataKey = "plan_id";
    private const string HomesteadSlug = "homestead";

    private readonly StripeOptions _opts;
    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<StripeBillingService> _logger;

    public StripeBillingService(
        IOptions<StripeOptions> opts,
        GoatLabDbContext db,
        ITenantContext tenantContext,
        ILogger<StripeBillingService> logger)
    {
        _opts = opts.Value;
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
        StripeConfiguration.ApiKey = _opts.SecretKey;
    }

    public async Task<string> CreateCheckoutSessionAsync(
        Tenant tenant,
        string userEmail,
        Plan targetPlan,
        string origin,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(targetPlan.StripePriceId))
            throw new InvalidOperationException($"Plan '{targetPlan.Slug}' has no Stripe price id configured.");

        // Create or reuse the Stripe customer.
        if (string.IsNullOrEmpty(tenant.StripeCustomerId))
        {
            var customers = new CustomerService();
            var customer = await customers.CreateAsync(new CustomerCreateOptions
            {
                Email = userEmail,
                Name = tenant.Name,
                Metadata = new Dictionary<string, string> { [TenantIdMetadataKey] = tenant.Id.ToString() },
            }, cancellationToken: cancellationToken);

            tenant.StripeCustomerId = customer.Id;
            _tenantContext.BypassFilter = true;
            try { await _db.SaveChangesAsync(cancellationToken); }
            finally { _tenantContext.BypassFilter = false; }
        }

        var sessionOptions = new SessionCreateOptions
        {
            Mode = "subscription",
            Customer = tenant.StripeCustomerId,
            LineItems = new List<SessionLineItemOptions>
            {
                new() { Price = targetPlan.StripePriceId, Quantity = 1 }
            },
            SuccessUrl = $"{origin.TrimEnd('/')}/billing?status=success&session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{origin.TrimEnd('/')}/billing?status=cancel",
            Metadata = new Dictionary<string, string>
            {
                [TenantIdMetadataKey] = tenant.Id.ToString(),
                [PlanIdMetadataKey] = targetPlan.Id.ToString(),
            },
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    [TenantIdMetadataKey] = tenant.Id.ToString(),
                    [PlanIdMetadataKey] = targetPlan.Id.ToString(),
                },
                TrialPeriodDays = targetPlan.TrialDays > 0 ? targetPlan.TrialDays : null,
            },
            AllowPromotionCodes = true,
        };

        var sessions = new SessionService();
        var session = await sessions.CreateAsync(sessionOptions, cancellationToken: cancellationToken);
        return session.Url;
    }

    public async Task<string> CreateCustomerPortalSessionAsync(
        Tenant tenant,
        string origin,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(tenant.StripeCustomerId))
            throw new InvalidOperationException("Tenant has no Stripe customer — subscribe first.");

        var portal = new Stripe.BillingPortal.SessionService();
        var session = await portal.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = tenant.StripeCustomerId,
            ReturnUrl = $"{origin.TrimEnd('/')}/billing",
        }, cancellationToken: cancellationToken);

        return session.Url;
    }

    public async Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken cancellationToken)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, _opts.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed");
            throw;
        }

        _logger.LogInformation("Received Stripe webhook {EventType} {EventId}", stripeEvent.Type, stripeEvent.Id);

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                if (stripeEvent.Data.Object is Session completedSession)
                    await OnCheckoutCompletedAsync(completedSession, cancellationToken);
                break;

            case "customer.subscription.created":
            case "customer.subscription.updated":
            case "customer.subscription.trial_will_end":
                if (stripeEvent.Data.Object is Subscription updatedSub)
                    await OnSubscriptionChangedAsync(updatedSub, cancellationToken);
                break;

            case "customer.subscription.deleted":
                if (stripeEvent.Data.Object is Subscription deletedSub)
                    await OnSubscriptionDeletedAsync(deletedSub, cancellationToken);
                break;

            default:
                break;
        }
    }

    private async Task OnCheckoutCompletedAsync(Session session, CancellationToken cancellationToken)
    {
        if (!TryGetIntMetadata(session.Metadata, TenantIdMetadataKey, out var tenantId)) return;

        var tenant = await LoadTenantAsync(tenantId, cancellationToken);
        if (tenant is null) return;

        tenant.StripeSubscriptionId = session.SubscriptionId;
        if (TryGetIntMetadata(session.Metadata, PlanIdMetadataKey, out var planId))
        {
            tenant.PlanId = planId;
        }

        await SaveAsync(cancellationToken);
    }

    private async Task OnSubscriptionChangedAsync(Subscription sub, CancellationToken cancellationToken)
    {
        if (!TryGetIntMetadata(sub.Metadata, TenantIdMetadataKey, out var tenantId)) return;

        var tenant = await LoadTenantAsync(tenantId, cancellationToken);
        if (tenant is null) return;

        tenant.StripeSubscriptionId = sub.Id;
        tenant.SubscriptionStatus = sub.Status;
        tenant.CurrentPeriodEnd = sub.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd;

        // Reset trial-reminder tracker if the trial window changed (new trial
        // started, trial extended, etc.) so TrialReminderJob sends fresh email.
        if (tenant.TrialEndsAt != sub.TrialEnd)
        {
            tenant.TrialReminderSentAt = null;
        }
        tenant.TrialEndsAt = sub.TrialEnd;

        if (TryGetIntMetadata(sub.Metadata, PlanIdMetadataKey, out var planId))
        {
            tenant.PlanId = planId;
        }

        await SaveAsync(cancellationToken);
    }

    private async Task OnSubscriptionDeletedAsync(Subscription sub, CancellationToken cancellationToken)
    {
        if (!TryGetIntMetadata(sub.Metadata, TenantIdMetadataKey, out var tenantId)) return;

        var tenant = await LoadTenantAsync(tenantId, cancellationToken);
        if (tenant is null) return;

        var freePlan = await _db.Plans.FirstOrDefaultAsync(p => p.Slug == HomesteadSlug, cancellationToken);
        if (freePlan is not null) tenant.PlanId = freePlan.Id;

        tenant.SubscriptionStatus = "canceled";
        tenant.StripeSubscriptionId = null;
        tenant.CurrentPeriodEnd = null;
        tenant.TrialEndsAt = null;

        await SaveAsync(cancellationToken);
    }

    private static bool TryGetIntMetadata(IDictionary<string, string>? metadata, string key, out int value)
    {
        value = 0;
        return metadata is not null
            && metadata.TryGetValue(key, out var v)
            && int.TryParse(v, out value);
    }

    private async Task<Tenant?> LoadTenantAsync(int tenantId, CancellationToken cancellationToken)
    {
        _tenantContext.BypassFilter = true;
        try
        {
            return await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        }
        finally { _tenantContext.BypassFilter = false; }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        _tenantContext.BypassFilter = true;
        try { await _db.SaveChangesAsync(cancellationToken); }
        finally { _tenantContext.BypassFilter = false; }
    }
}
