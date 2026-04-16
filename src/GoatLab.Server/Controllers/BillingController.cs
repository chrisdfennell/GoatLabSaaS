using System.Security.Claims;
using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services;
using GoatLab.Server.Services.Billing;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BillingController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IBillingService _billing;
    private readonly UserManager<ApplicationUser> _userManager;

    public BillingController(
        GoatLabDbContext db,
        ITenantContext tenantContext,
        IBillingService billing,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _tenantContext = tenantContext;
        _billing = billing;
        _userManager = userManager;
    }

    public record BillingStatusDto(
        int PlanId,
        string PlanName,
        string PlanSlug,
        int PlanPriceMonthlyCents,
        string? Status,
        DateTime? TrialEndsAt,
        DateTime? CurrentPeriodEnd,
        bool HasStripeCustomer);

    public record UsageDto(
        string PlanName,
        int GoatCount,
        int? MaxGoats,
        int UserCount,
        int? MaxUsers);

    public record CheckoutRequest(int PlanId);

    public record RedirectUrlResponse(string Url);

    [HttpGet("usage")]
    public async Task<ActionResult<UsageDto>> GetUsage(CancellationToken ct)
    {
        if (_tenantContext.TenantId is not int tenantId) return NotFound();

        var tenant = await _db.Tenants.Include(t => t.Plan).FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant?.Plan is null) return Problem("Tenant has no plan assigned.");

        var goatCount = await _db.Goats.CountAsync(g => g.TenantId == tenantId, ct);

        _tenantContext.BypassFilter = true;
        int userCount;
        try { userCount = await _db.TenantMembers.CountAsync(m => m.TenantId == tenantId, ct); }
        finally { _tenantContext.BypassFilter = false; }

        return new UsageDto(tenant.Plan.Name, goatCount, tenant.Plan.MaxGoats, userCount, tenant.Plan.MaxUsers);
    }

    [HttpGet("status")]
    public async Task<ActionResult<BillingStatusDto>> GetStatus(CancellationToken ct)
    {
        var tenant = await CurrentTenantAsync(ct);
        if (tenant is null) return NotFound();
        if (tenant.Plan is null) return Problem("Tenant has no plan assigned.");

        return new BillingStatusDto(
            tenant.Plan.Id,
            tenant.Plan.Name,
            tenant.Plan.Slug,
            tenant.Plan.PriceMonthlyCents,
            tenant.SubscriptionStatus,
            tenant.TrialEndsAt,
            tenant.CurrentPeriodEnd,
            !string.IsNullOrEmpty(tenant.StripeCustomerId));
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<RedirectUrlResponse>> Checkout([FromBody] CheckoutRequest req, CancellationToken ct)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == req.PlanId && p.IsActive, ct);
        if (plan is null) return BadRequest(new { error = "Unknown or inactive plan." });
        if (plan.PriceMonthlyCents == 0 || string.IsNullOrEmpty(plan.StripePriceId))
            return BadRequest(new { error = "This plan is free — no checkout required." });

        var tenant = await CurrentTenantAsync(ct);
        if (tenant is null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (user?.Email is null) return Unauthorized();

        var url = await _billing.CreateCheckoutSessionAsync(tenant, user.Email, plan, RequestOrigin(), ct);
        return new RedirectUrlResponse(url);
    }

    [HttpPost("portal")]
    public async Task<ActionResult<RedirectUrlResponse>> Portal(CancellationToken ct)
    {
        var tenant = await CurrentTenantAsync(ct);
        if (tenant is null) return NotFound();
        if (string.IsNullOrEmpty(tenant.StripeCustomerId))
            return BadRequest(new { error = "No Stripe customer yet — subscribe first." });

        var url = await _billing.CreateCustomerPortalSessionAsync(tenant, RequestOrigin(), ct);
        return new RedirectUrlResponse(url);
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        try
        {
            await _billing.HandleWebhookAsync(payload, signature, ct);
            return Ok();
        }
        catch (Stripe.StripeException)
        {
            return BadRequest();
        }
    }

    private async Task<Tenant?> CurrentTenantAsync(CancellationToken ct)
    {
        if (_tenantContext.TenantId is not int id) return null;
        return await _db.Tenants.Include(t => t.Plan).FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    private string RequestOrigin()
    {
        var origin = Request.Headers["Origin"].ToString();
        if (!string.IsNullOrEmpty(origin)) return origin;
        return $"{Request.Scheme}://{Request.Host}";
    }
}
