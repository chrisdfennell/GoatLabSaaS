using System.Security.Cryptography;
using System.Text;
using GoatLab.Server.Data;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Vet;

// Mints + validates time-bounded share links the owner sends to a vet.
// Tokens follow the GoatTransfer pattern: plaintext "vs_" + base64url(32),
// only the SHA-256 hash is persisted, plaintext returned to the seller once.
public class VetShareLinkService
{
    public const string TokenPrefix = "vs_";

    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;

    public VetShareLinkService(GoatLabDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public record CreateInput(int GoatId, int ExpiresInDays, string? VetName, string? VetEmail);
    public record CreatedLinkDto(int Id, string Token, DateTime ExpiresAt, string? VetName);

    public async Task<CreatedLinkDto> CreateAsync(CreateInput input, string userId, CancellationToken ct)
    {
        // Tenant filter is on (caller is authed); confirm goat is in scope.
        var goat = await _db.Goats.FirstOrDefaultAsync(g => g.Id == input.GoatId, ct)
                   ?? throw new InvalidOperationException("Goat not found in your tenant.");

        var days = Math.Clamp(input.ExpiresInDays, 1, 60);
        var (plaintext, hash) = GenerateToken();

        var link = new VetShareLink
        {
            TenantId = goat.TenantId,
            GoatId = goat.Id,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(days),
            TokenPrefix = plaintext[..12],
            TokenHash = hash,
            VetName = string.IsNullOrWhiteSpace(input.VetName) ? null : input.VetName.Trim(),
            VetEmail = string.IsNullOrWhiteSpace(input.VetEmail) ? null : input.VetEmail.Trim().ToLowerInvariant(),
        };
        _db.VetShareLinks.Add(link);
        await _db.SaveChangesAsync(ct);

        return new CreatedLinkDto(link.Id, plaintext, link.ExpiresAt, link.VetName);
    }

    public record VetShareSummary(
        int Id, string TokenPrefix, DateTime CreatedAt, DateTime ExpiresAt,
        DateTime? LastViewedAt, DateTime? RevokedAt, string? VetName, string? VetEmail);

    public async Task<List<VetShareSummary>> ListForGoatAsync(int goatId, CancellationToken ct) =>
        await _db.VetShareLinks
            .Where(v => v.GoatId == goatId)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new VetShareSummary(
                v.Id, v.TokenPrefix, v.CreatedAt, v.ExpiresAt,
                v.LastViewedAt, v.RevokedAt, v.VetName, v.VetEmail))
            .ToListAsync(ct);

    public async Task<bool> RevokeAsync(int id, CancellationToken ct)
    {
        var link = await _db.VetShareLinks.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (link is null) return false;
        if (link.RevokedAt is null)
        {
            link.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return true;
    }

    // Public lookup — anonymous request, must verify token + expiry. Uses
    // BypassFilter because anon callers have no tenant claim.
    public async Task<VetShareLink?> ResolveTokenAsync(string plaintext, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(plaintext) || !plaintext.StartsWith(TokenPrefix)) return null;
        var hash = HashToken(plaintext);

        _tenantContext.BypassFilter = true;
        var link = await _db.VetShareLinks
            .Include(v => v.Goat)
            .Include(v => v.Tenant)
            .FirstOrDefaultAsync(v => v.TokenHash == hash, ct);
        if (link is null) return null;

        var now = DateTime.UtcNow;
        if (link.RevokedAt is not null) return null;
        if (link.ExpiresAt < now) return null;

        return link;
    }

    public async Task RecordViewAsync(int linkId, CancellationToken ct)
    {
        _tenantContext.BypassFilter = true;
        await _db.VetShareLinks
            .Where(v => v.Id == linkId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.LastViewedAt, DateTime.UtcNow), ct);
    }

    private static (string plaintext, string hash) GenerateToken()
    {
        Span<byte> buf = stackalloc byte[32];
        RandomNumberGenerator.Fill(buf);
        // base64url, no padding
        var raw = Convert.ToBase64String(buf)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var plaintext = TokenPrefix + raw;
        var hash = HashToken(plaintext);
        return (plaintext, hash);
    }

    private static string HashToken(string plaintext)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var digest = SHA256.HashData(bytes);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
