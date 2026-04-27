using GoatLab.Server.Data;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

// Disambiguate Plan — we mean the app's subscription plan entity, not Stripe's
// legacy Plan object (which has been superseded by Price on Stripe's side anyway).
using Plan = GoatLab.Shared.Models.Plan;
// Customer is our buyer entity, not Stripe.Customer.
using Customer = GoatLab.Shared.Models.Customer;

namespace GoatLab.Server.Services.Billing;

public class StripeBillingService : IBillingService
{
    private const string TenantIdMetadataKey = "tenant_id";
    private const string PlanIdMetadataKey = "plan_id";
    private const string HomesteadSlug = "homestead";

    // Keys for the public-listing deposit flow. Presence of DepositGoatIdKey in
    // checkout session metadata distinguishes a buyer deposit from a tenant
    // subscription purchase inside the webhook.
    private const string DepositGoatIdKey = "deposit_goat_id";
    private const string DepositBuyerEmailKey = "deposit_buyer_email";
    private const string DepositBuyerNameKey = "deposit_buyer_name";
    private const string DepositBuyerPhoneKey = "deposit_buyer_phone";
    private const string DepositNotesKey = "deposit_notes";

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

    public async Task<string> CreateDepositCheckoutSessionAsync(
        Tenant tenant,
        Goat goat,
        int depositCents,
        PublicReservationRequest request,
        string origin,
        CancellationToken cancellationToken)
    {
        var productName = $"Deposit — {goat.Name}";
        var description = $"Reservation deposit for {goat.Name} at {tenant.Name}. Applied toward the final purchase price; refundable per the farm's policy.";

        var sessionOptions = new SessionCreateOptions
        {
            Mode = "payment",
            CustomerEmail = request.BuyerEmail,
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "usd",
                        UnitAmount = depositCents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = productName,
                            Description = description,
                        },
                    },
                },
            },
            SuccessUrl = $"{origin.TrimEnd('/')}/pub/{tenant.Slug}/{goat.Id}?deposit=success",
            CancelUrl = $"{origin.TrimEnd('/')}/pub/{tenant.Slug}/{goat.Id}?deposit=cancel",
            Metadata = new Dictionary<string, string>
            {
                [TenantIdMetadataKey] = tenant.Id.ToString(),
                [DepositGoatIdKey] = goat.Id.ToString(),
                [DepositBuyerEmailKey] = Truncate(request.BuyerEmail, 120),
                [DepositBuyerNameKey] = Truncate(request.BuyerName, 100),
                [DepositBuyerPhoneKey] = Truncate(request.BuyerPhone ?? "", 40),
                [DepositNotesKey] = Truncate(request.Notes ?? "", 500),
            },
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Description = $"GoatLab deposit — {tenant.Name} — {goat.Name} (#{goat.Id})",
                Metadata = new Dictionary<string, string>
                {
                    [TenantIdMetadataKey] = tenant.Id.ToString(),
                    [DepositGoatIdKey] = goat.Id.ToString(),
                },
            },
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
        await DispatchEventAsync(stripeEvent, cancellationToken);
    }

    // Switchboard for both the live webhook handler and the super-admin replay
    // path. Caller is responsible for trust (signature verification on the
    // live path; super-admin auth on the replay path).
    private async Task<bool> DispatchEventAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                if (stripeEvent.Data.Object is Session completedSession)
                    await OnCheckoutCompletedAsync(completedSession, cancellationToken);
                return true;

            case "customer.subscription.created":
            case "customer.subscription.updated":
            case "customer.subscription.trial_will_end":
                if (stripeEvent.Data.Object is Subscription updatedSub)
                    await OnSubscriptionChangedAsync(updatedSub, cancellationToken);
                return true;

            case "customer.subscription.deleted":
                if (stripeEvent.Data.Object is Subscription deletedSub)
                    await OnSubscriptionDeletedAsync(deletedSub, cancellationToken);
                return true;

            default:
                return false;
        }
    }

    public async Task<StripeSyncResultDto> SyncTenantFromStripeAsync(int tenantId, CancellationToken cancellationToken)
    {
        var tenant = await LoadTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
            return new StripeSyncResultDto(tenantId, "?", false, "Tenant not found.", Array.Empty<string>());
        if (string.IsNullOrEmpty(_opts.SecretKey))
            return new StripeSyncResultDto(tenantId, tenant.Slug, false, "Stripe is not configured (no secret key).", Array.Empty<string>());

        Subscription? sub = null;
        try
        {
            var subSvc = new SubscriptionService();
            if (!string.IsNullOrEmpty(tenant.StripeSubscriptionId))
            {
                sub = await subSvc.GetAsync(tenant.StripeSubscriptionId, cancellationToken: cancellationToken);
            }
            else if (!string.IsNullOrEmpty(tenant.StripeCustomerId))
            {
                // No sub id locally — list and pick the most recent active sub
                // for this customer. Could be a subscription created by Stripe
                // CLI or a CSV import, never wired back into our DB.
                var list = await subSvc.ListAsync(new SubscriptionListOptions
                {
                    Customer = tenant.StripeCustomerId,
                    Limit = 5,
                    Status = "all"
                }, cancellationToken: cancellationToken);
                sub = list.Data
                    .OrderByDescending(s => s.Created)
                    .FirstOrDefault(s => s.Status is "active" or "trialing" or "past_due")
                    ?? list.Data.OrderByDescending(s => s.Created).FirstOrDefault();
            }
        }
        catch (StripeException ex)
        {
            return new StripeSyncResultDto(tenantId, tenant.Slug, false, $"Stripe API error: {ex.Message}", Array.Empty<string>());
        }

        if (sub is null)
            return new StripeSyncResultDto(tenantId, tenant.Slug, false,
                "No subscription found in Stripe for this tenant.", Array.Empty<string>());

        var changes = new List<string>();

        if (tenant.StripeSubscriptionId != sub.Id)
        {
            changes.Add($"subscription id: {tenant.StripeSubscriptionId ?? "(none)"} → {sub.Id}");
            tenant.StripeSubscriptionId = sub.Id;
        }
        if (tenant.SubscriptionStatus != sub.Status)
        {
            changes.Add($"status: {tenant.SubscriptionStatus ?? "(none)"} → {sub.Status}");
            tenant.SubscriptionStatus = sub.Status;
        }
        var stripePeriodEnd = sub.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd;
        if (tenant.CurrentPeriodEnd != stripePeriodEnd)
        {
            changes.Add($"current_period_end: {tenant.CurrentPeriodEnd:o} → {stripePeriodEnd:o}");
            tenant.CurrentPeriodEnd = stripePeriodEnd;
        }
        if (tenant.TrialEndsAt != sub.TrialEnd)
        {
            changes.Add($"trial_end: {tenant.TrialEndsAt:o} → {sub.TrialEnd:o}");
            tenant.TrialEndsAt = sub.TrialEnd;
            tenant.TrialReminderSentAt = null; // give the reminder job a clean slate
        }

        // Match the Stripe Price back to a local Plan via StripePriceId.
        var stripePriceId = sub.Items?.Data?.FirstOrDefault()?.Price?.Id;
        if (!string.IsNullOrEmpty(stripePriceId))
        {
            var matchedPlan = await _db.Plans.AsNoTracking()
                .FirstOrDefaultAsync(p => p.StripePriceId == stripePriceId, cancellationToken);
            if (matchedPlan is not null && matchedPlan.Id != tenant.PlanId)
            {
                changes.Add($"plan: {tenant.PlanId} → {matchedPlan.Id} ({matchedPlan.Slug})");
                tenant.PlanId = matchedPlan.Id;
            }
        }

        if (changes.Count > 0)
        {
            await SaveAsync(cancellationToken);
        }

        var msg = changes.Count == 0
            ? "Already in sync — no changes."
            : $"Applied {changes.Count} change(s).";
        return new StripeSyncResultDto(tenantId, tenant.Slug, true, msg, changes);
    }

    public async Task<StripeReplayResultDto> ReplayStripeEventAsync(string eventId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_opts.SecretKey))
            return new StripeReplayResultDto(eventId, "?", false, "Stripe is not configured (no secret key).");

        Event stripeEvent;
        try
        {
            var eventSvc = new EventService();
            stripeEvent = await eventSvc.GetAsync(eventId, cancellationToken: cancellationToken);
        }
        catch (StripeException ex)
        {
            return new StripeReplayResultDto(eventId, "?", false, $"Stripe API error: {ex.Message}");
        }

        _logger.LogWarning("Super-admin manual replay of Stripe event {EventType} {EventId}",
            stripeEvent.Type, stripeEvent.Id);

        var handled = await DispatchEventAsync(stripeEvent, cancellationToken);
        return new StripeReplayResultDto(stripeEvent.Id, stripeEvent.Type, handled,
            handled ? "Event re-dispatched." : "No handler for this event type — nothing to do.");
    }

    private async Task OnCheckoutCompletedAsync(Session session, CancellationToken cancellationToken)
    {
        if (!TryGetIntMetadata(session.Metadata, TenantIdMetadataKey, out var tenantId)) return;

        // Buyer-deposit flow: metadata carries a goat id. We don't touch the
        // subscription fields here — it's a one-time payment unrelated to the
        // tenant's own plan. Instead we upsert a Customer and a WaitlistEntry.
        if (TryGetIntMetadata(session.Metadata, DepositGoatIdKey, out var depositGoatId))
        {
            await OnDepositCompletedAsync(tenantId, depositGoatId, session, cancellationToken);
            return;
        }

        var tenant = await LoadTenantAsync(tenantId, cancellationToken);
        if (tenant is null) return;

        tenant.StripeSubscriptionId = session.SubscriptionId;
        if (TryGetIntMetadata(session.Metadata, PlanIdMetadataKey, out var planId))
        {
            tenant.PlanId = planId;
        }

        await SaveAsync(cancellationToken);
    }

    private async Task OnDepositCompletedAsync(
        int tenantId,
        int goatId,
        Session session,
        CancellationToken cancellationToken)
    {
        var metadata = session.Metadata ?? new Dictionary<string, string>();
        metadata.TryGetValue(DepositBuyerEmailKey, out var email);
        metadata.TryGetValue(DepositBuyerNameKey, out var name);
        metadata.TryGetValue(DepositBuyerPhoneKey, out var phone);
        metadata.TryGetValue(DepositNotesKey, out var notes);

        email = string.IsNullOrWhiteSpace(email) ? session.CustomerEmail : email;
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Deposit session {Session} for tenant {Tenant} goat {Goat} has no buyer email — skipping.",
                session.Id, tenantId, goatId);
            return;
        }

        var amountCents = session.AmountTotal is long t ? (int)t : 0;

        _tenantContext.BypassFilter = true;
        try
        {
            var goat = await _db.Goats.FirstOrDefaultAsync(g => g.Id == goatId && g.TenantId == tenantId, cancellationToken);
            if (goat is null)
            {
                _logger.LogWarning("Deposit session {Session} references missing goat {Goat} in tenant {Tenant}.",
                    session.Id, goatId, tenantId);
                return;
            }

            // Idempotency: Stripe webhooks can fire twice. Key off the session id
            // stored in WaitlistEntry.Notes so a retry doesn't double-book.
            var existing = await _db.WaitlistEntries
                .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Notes != null && w.Notes.Contains(session.Id), cancellationToken);
            if (existing is not null) return;

            var customer = await _db.Customers
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Email == email, cancellationToken);
            if (customer is null)
            {
                customer = new Customer
                {
                    TenantId = tenantId,
                    Name = string.IsNullOrWhiteSpace(name) ? email : name,
                    Email = email,
                    Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                    Notes = "Created from public-listing deposit.",
                    CreatedAt = DateTime.UtcNow,
                };
                _db.Customers.Add(customer);
                await _db.SaveChangesAsync(cancellationToken);
            }

            var noteText = $"Deposit via public listing for goat #{goat.Id} ({goat.Name}). Stripe session {session.Id}.";
            if (!string.IsNullOrWhiteSpace(notes)) noteText += $"\nBuyer notes: {notes}";

            var entry = new WaitlistEntry
            {
                TenantId = tenantId,
                CustomerId = customer.Id,
                DepositCents = amountCents,
                DepositPaid = true,
                DepositReceivedAt = DateTime.UtcNow,
                Priority = 0,
                Status = WaitlistStatus.Waiting,
                Notes = noteText,
                CreatedAt = DateTime.UtcNow,
            };
            _db.WaitlistEntries.Add(entry);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created WaitlistEntry {Id} for deposit session {Session} (tenant {Tenant}, goat {Goat}).",
                entry.Id, session.Id, tenantId, goatId);
        }
        finally { _tenantContext.BypassFilter = false; }
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

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];

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
