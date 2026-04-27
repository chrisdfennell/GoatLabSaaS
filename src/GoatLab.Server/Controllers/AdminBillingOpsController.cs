using GoatLab.Server.Services;
using GoatLab.Server.Services.Billing;
using GoatLab.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoatLab.Server.Controllers;

// Super-admin Stripe ops: drift repair (resync a tenant from Stripe) and
// webhook replay (re-dispatch a Stripe event by id). Audited via
// IAdminAuditLogger so any state change is attributable.
[ApiController]
[Route("api/admin/billing")]
[Authorize(Policy = SuperAdminPolicy.Name)]
public class AdminBillingOpsController : ControllerBase
{
    private readonly IBillingService _billing;
    private readonly IAdminAuditLogger _audit;

    public AdminBillingOpsController(IBillingService billing, IAdminAuditLogger audit)
    {
        _billing = billing;
        _audit = audit;
    }

    [HttpPost("sync/{tenantId:int}")]
    public async Task<ActionResult<StripeSyncResultDto>> Sync(int tenantId, CancellationToken ct)
    {
        var result = await _billing.SyncTenantFromStripeAsync(tenantId, ct);
        if (result.Found && result.Changes.Count > 0)
        {
            await _audit.LogAsync(
                action: "stripe.sync",
                targetType: "Tenant",
                targetId: tenantId.ToString(),
                detail: $"{result.Changes.Count} change(s): {string.Join("; ", result.Changes)}");
        }
        return result;
    }

    [HttpPost("replay/{eventId}")]
    public async Task<ActionResult<StripeReplayResultDto>> Replay(string eventId, CancellationToken ct)
    {
        var result = await _billing.ReplayStripeEventAsync(eventId, ct);
        if (result.Handled)
        {
            await _audit.LogAsync(
                action: "stripe.replay",
                targetType: "StripeEvent",
                targetId: eventId,
                detail: $"Re-dispatched {result.EventType}");
        }
        return result;
    }
}
