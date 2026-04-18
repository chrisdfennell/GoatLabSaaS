using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

// Outbound webhook subscription. For each configured event the tenant is
// subscribed to (comma-separated list in Events, e.g. "goat.created,sale.updated"),
// the dispatcher POSTs a JSON payload to Url with a HMAC-SHA256 signature
// computed using Secret. See WebhookDispatcher + WebhookRetryJob.
public class Webhook : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Url { get; set; } = string.Empty;

    // Random hex used to sign payloads. Shown to the user once at creation —
    // they need it to verify incoming webhook signatures on their receiver.
    [Required, MaxLength(64)]
    public string Secret { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Events { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastDeliveredAt { get; set; }
    public int? LastStatusCode { get; set; }

    [MaxLength(1000)]
    public string? LastError { get; set; }
}
