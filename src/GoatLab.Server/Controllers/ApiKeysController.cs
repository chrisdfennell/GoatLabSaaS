using System.Security.Claims;
using GoatLab.Server.Data;
using GoatLab.Server.Services;
using GoatLab.Server.Services.ApiKeys;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Tenant API key management. Cookie-scheme only — an API-key-authenticated
// session can't mint or revoke keys, which keeps a leaked key from
// self-propagating.
[ApiController]
[Route("api/[controller]")]
// "Identity.Application" is the value of IdentityConstants.ApplicationScheme —
// attribute args must be constants, so we inline the string. Pin to this one
// scheme so a leaked API key can't create more keys.
[Authorize(AuthenticationSchemes = "Identity.Application")]
[RequiresFeature(AppFeature.WebhooksAndApi)]
public class ApiKeysController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ApiKeysController> _logger;

    public ApiKeysController(
        GoatLabDbContext db,
        ITenantContext tenantContext,
        ILogger<ApiKeysController> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public record ApiKeySummaryDto(
        int Id, string Name, string Prefix,
        DateTime CreatedAt, DateTime? LastUsedAt, DateTime? ExpiresAt);

    public record CreatedKeyDto(
        int Id, string Name, string Prefix,
        DateTime CreatedAt, DateTime? ExpiresAt,
        string PlaintextKey);

    [HttpGet]
    public async Task<ActionResult<List<ApiKeySummaryDto>>> List()
    {
        var keys = await _db.ApiKeys
            .Where(k => k.RevokedAt == null)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeySummaryDto(k.Id, k.Name, k.Prefix, k.CreatedAt, k.LastUsedAt, k.ExpiresAt))
            .ToListAsync();
        return keys;
    }

    public record CreateRequest([property: System.ComponentModel.DataAnnotations.Required] string Name, DateTime? ExpiresAt);

    [HttpPost]
    public async Task<ActionResult<CreatedKeyDto>> Create([FromBody] CreateRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Name is required." });
        if (req.Name.Length > 100)
            return BadRequest(new { error = "Name must be 100 characters or fewer." });

        // Without an active tenant, TenantId stamping at SaveChanges would leave
        // the FK as 0 and the DB would reject with an opaque 500. Surface early.
        if (_tenantContext.TenantId is not int tenantId)
            return BadRequest(new { error = "No farm selected. Pick a farm first." });

        var generated = ApiKeyGenerator.Generate();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var key = new ApiKey
        {
            TenantId = tenantId,          // set explicitly so we don't depend on SaveChanges stamping.
            Name = req.Name.Trim(),
            Prefix = generated.Prefix,
            KeyHash = generated.KeyHash,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = req.ExpiresAt,
        };
        _db.ApiKeys.Add(key);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to create API key for tenant {Tenant} user {User}", tenantId, userId);
            return Problem(
                title: "Could not create API key",
                detail: ex.InnerException?.Message ?? ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return new CreatedKeyDto(key.Id, key.Name, key.Prefix, key.CreatedAt, key.ExpiresAt, generated.Plaintext);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Revoke(int id)
    {
        var key = await _db.ApiKeys.FindAsync(id);
        if (key is null) return NotFound();
        if (key.RevokedAt != null) return NoContent();

        key.RevokedAt = DateTime.UtcNow;
        key.RevokedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
