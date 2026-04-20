using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

// Persisted record of one webhook delivery attempt. Rows live until either
// DeliveredAt is set (success, 2xx) or AttemptCount hits the retry cap. The
// retry job sweeps undelivered rows where NextRetryAt has elapsed.
public class WebhookDelivery : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int WebhookId { get; set; }
    public Webhook? Webhook { get; set; }

    [Required, MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    // Stable identifier the receiver can use for idempotency (X-GoatLab-Delivery).
    [Required, MaxLength(36)]
    public string DeliveryId { get; set; } = Guid.NewGuid().ToString();

    // Serialized JSON payload — bounded only by nvarchar(max).
    [Required]
    public string Payload { get; set; } = string.Empty;

    public int AttemptCount { get; set; }
    public int? StatusCode { get; set; }

    [MaxLength(500)]
    public string? ResponseBody { get; set; }

    [MaxLength(1000)]
    public string? Error { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
    public DateTime? NextRetryAt { get; set; }
}
