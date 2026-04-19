using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services.Transfers;
using GoatLab.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/transfers")]
public class GoatTransfersController : ControllerBase
{
    private readonly GoatTransferService _svc;
    private readonly UserManager<ApplicationUser> _userManager;

    public GoatTransfersController(GoatTransferService svc, UserManager<ApplicationUser> userManager)
    {
        _svc = svc;
        _userManager = userManager;
    }

    // Seller initiates (cookie auth — must be logged in inside the source tenant).
    [HttpPost]
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    [EnableRateLimiting("transfer")]
    public async Task<ActionResult<InitiateTransferResponse>> Initiate(
        [FromBody] InitiateTransferRequest req,
        CancellationToken ct)
    {
        if (req is null || req.GoatId <= 0 || string.IsNullOrWhiteSpace(req.BuyerEmail))
            return BadRequest(new { error = "GoatId and buyer email are required." });

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        try
        {
            var resp = await _svc.InitiateAsync(
                req.GoatId,
                req.BuyerEmail,
                req.Message,
                req.ExpiryDays,
                user.Id,
                RequestOrigin(),
                ct);
            return resp is null ? NotFound() : Ok(resp);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpGet]
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    public async Task<ActionResult<IReadOnlyList<GoatTransferSummaryDto>>> ListForSeller(CancellationToken ct)
    {
        var list = await _svc.ListForSellerAsync(ct);
        return Ok(list);
    }

    [HttpDelete("{id:int}")]
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var ok = await _svc.CancelAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    // ---------- Buyer-side (magic-link token) ----------

    // Anon: preview what's on offer. Callers must know the plaintext token.
    [HttpGet("token/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<GoatTransferPreviewDto>> Preview(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return NotFound();
        var preview = await _svc.PreviewByTokenAsync(token, ct);
        return preview is null ? NotFound() : Ok(preview);
    }

    [HttpPost("token/{token}/accept")]
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    public async Task<ActionResult<AcceptTransferResponse>> Accept(
        string token,
        [FromBody] AcceptTransferRequest req,
        CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        if (req is null || req.ToTenantId <= 0) return BadRequest(new { error = "ToTenantId is required." });

        try
        {
            var resp = await _svc.AcceptAsync(token, req.ToTenantId, user.Id, ct);
            return resp is null ? NotFound() : Ok(resp);
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    // Decline can be anonymous — the buyer may not have a GoatLab account.
    [HttpPost("token/{token}/decline")]
    [AllowAnonymous]
    public async Task<IActionResult> Decline(string token, [FromBody] DeclineTransferRequest req, CancellationToken ct)
    {
        var ok = await _svc.DeclineByTokenAsync(token, req?.Reason, ct);
        return ok ? NoContent() : NotFound();
    }

    private string RequestOrigin()
    {
        var origin = Request.Headers["Origin"].ToString();
        if (!string.IsNullOrEmpty(origin)) return origin;
        return $"{Request.Scheme}://{Request.Host}";
    }
}
