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

    /// <summary>Verifies the Stripe signature on the webhook and applies state changes to the Tenant row.</summary>
    Task HandleWebhookAsync(
        string payload,
        string signatureHeader,
        CancellationToken cancellationToken);
}
