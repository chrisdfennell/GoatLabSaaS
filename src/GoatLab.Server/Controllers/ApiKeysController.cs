using System.Security.Claims;
using GoatLab.Server.Data;
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
    public ApiKeysController(GoatLabDbContext db) => _db = db;

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
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Name is required." });

        var generated = ApiKeyGenerator.Generate();

        var key = new ApiKey
        {
            Name = req.Name.Trim(),
            Prefix = generated.Prefix,
            KeyHash = generated.KeyHash,
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = req.ExpiresAt,
        };
        _db.ApiKeys.Add(key);
        await _db.SaveChangesAsync();

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
