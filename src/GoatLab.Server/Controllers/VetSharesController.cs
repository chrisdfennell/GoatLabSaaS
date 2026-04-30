using System.Security.Claims;
using GoatLab.Server.Data;
using GoatLab.Server.Services.Email;
using GoatLab.Server.Services.Vet;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Owner-side CRUD for vet share links. Cookie-only — API keys can't mint
// share links, since that would let a tenant-owner downgrade auth without
// realizing it.
[ApiController]
[Route("api/vet-shares")]
[Authorize(AuthenticationSchemes = "Identity.Application")]
public class VetSharesController : ControllerBase
{
    private readonly VetShareLinkService _service;
    private readonly IAppEmailSender _email;
    private readonly IConfiguration _config;
    private readonly ILogger<VetSharesController> _logger;

    public VetSharesController(VetShareLinkService service, IAppEmailSender email, IConfiguration config, ILogger<VetSharesController> logger)
    {
        _service = service;
        _email = email;
        _config = config;
        _logger = logger;
    }

    public record CreateRequest(int GoatId, int ExpiresInDays = 14, string? VetName = null, string? VetEmail = null);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int goatId, CancellationToken ct)
    {
        if (goatId <= 0) return BadRequest("goatId required.");
        var rows = await _service.ListForGoatAsync(goatId, ct);
        return Ok(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest req, CancellationToken ct)
    {
        if (req.GoatId <= 0) return BadRequest("GoatId required.");
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        VetShareLinkService.CreatedLinkDto created;
        try
        {
            created = await _service.CreateAsync(
                new VetShareLinkService.CreateInput(req.GoatId, req.ExpiresInDays, req.VetName, req.VetEmail),
                userId, ct);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }

        // If the user gave us the vet's email, send the link directly. Plaintext
        // token is in the URL only — never stored anywhere except the email body.
        if (!string.IsNullOrWhiteSpace(req.VetEmail))
        {
            try
            {
                var origin = (_config["PublicOrigin"] ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');
                var url = $"{origin}/vet/{created.Token}";
                var (subject, html, text) = EmailTemplates.VetShareInvite(
                    vetName: created.VetName ?? "Doctor",
                    url: url,
                    expiresAt: created.ExpiresAt);
                await _email.SendAsync(req.VetEmail.Trim(), subject, html, text, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Vet share invite email failed for link {Id}", created.Id);
            }
        }

        return Ok(created);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Revoke(int id, CancellationToken ct)
    {
        var ok = await _service.RevokeAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}
