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
}
