using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

// Tenant-scoped API key. Plaintext is `gl_<32-byte-base64url>` and only ever
// returned once at creation. The server stores SHA-256(plaintext) as KeyHash
// (unique) and a 12-char Prefix for display in the UI so users can spot the
// right row when revoking. Bearer auth uses the ApiKey scheme (see
// Services/Auth/ApiKeyAuthHandler) — keys land in the same tenant context as
// the user who created them.
public class ApiKey : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(12)]
    public string Prefix { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string KeyHash { get; set; } = string.Empty;

    [Required, MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    [MaxLength(450)]
    public string? RevokedByUserId { get; set; }
}
