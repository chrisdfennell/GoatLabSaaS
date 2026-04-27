using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;

namespace GoatLab.Server.Services.Billing;

public interface IBillingService
{
    /// <summary>Creates a Stripe Checkout session for the tenant to subscribe to the given paid plan.</summary>
    /// <returns>Checkout URL the caller should redirect the browser to.</returns>
    Task<string> CreateCheckoutSessionAsync(
        Tenant tenant,
        string userEmail,
        Plan targetPlan,
        string origin,
        CancellationToken cancellationToken);

    /// <summary>Creates a Stripe Customer Portal session for self-service plan/card/cancel management.</summary>
    Task<string> CreateCustomerPortalSessionAsync(
        Tenant tenant,
        string origin,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a Stripe one-time payment Checkout session for a buyer reserving
    /// <paramref name="goat"/> on the public listing page. Metadata wires the
    /// webhook back to the tenant, goat, and buyer so we can create a Customer
    /// + WaitlistEntry when payment succeeds.
    /// </summary>
    Task<string> CreateDepositCheckoutSessionAsync(
        Tenant tenant,
        Goat goat,
        int depositCents,
        PublicReservationRequest request,
        string origin,
        CancellationToken cancellationToken);

    /// <summary>Verifies the Stripe signature on the webhook and applies state changes to the Tenant row.</summary>
    Task HandleWebhookAsync(
        string payload,
        string signatureHeader,
        CancellationToken cancellationToken);

    /// <summary>
    /// Super-admin drift repair. Pulls the tenant's subscription state from
    /// Stripe and overwrites the local tenant row so SubscriptionStatus,
    /// CurrentPeriodEnd, TrialEndsAt, and PlanId match Stripe.
    /// </summary>
    /// <returns>List of human-readable change descriptions ("status: trialing → active") and a top-level message.</returns>
    Task<StripeSyncResultDto> SyncTenantFromStripeAsync(int tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Super-admin webhook replay. Fetches a Stripe event by id from the API
    /// (no signature verification — caller is super-admin) and dispatches it
    /// through the same handler the live webhook uses.
    /// </summary>
    Task<StripeReplayResultDto> ReplayStripeEventAsync(string eventId, CancellationToken cancellationToken);
}
