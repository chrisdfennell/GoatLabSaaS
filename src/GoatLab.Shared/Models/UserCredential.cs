using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

// A WebAuthn passkey / FIDO2 credential bound to an ApplicationUser.
// Stored per-registration; a user may have multiple (phone, laptop, hardware key).
// Not ITenantOwned — passkeys belong to users, not farms.
public class UserCredential
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    // Raw WebAuthn credential id. Unique across the authenticator population;
    // we index for O(1) lookup during assertion (login).
    [Required]
    public byte[] CredentialId { get; set; } = Array.Empty<byte>();

    [Required]
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();

    // Spec-defined monotonic counter. Used to detect cloned authenticators —
    // if the assertion counter ever decreases, that's a red flag.
    public uint SignCount { get; set; }

    // User-friendly name, e.g., "Chris's iPhone", "YubiKey 5C". Defaults to
    // the authenticator AAGUID model name when the UI can't identify it.
    [MaxLength(100)]
    public string Name { get; set; } = "Security key";

    // Identifies the authenticator model — useful for display and policy.
    public Guid AaGuid { get; set; }

    // Spec: "usb", "nfc", "ble", "internal", "hybrid" — comma-joined.
    [MaxLength(100)]
    public string? Transports { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}
