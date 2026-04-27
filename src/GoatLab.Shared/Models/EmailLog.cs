using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

// One row per outbound email send attempt. Lets super-admin answer the
// "did the email send?" support ticket without grepping container logs or
// the Brevo dashboard. Bodies are NOT stored — too much noise + PII for what
// a support log needs. Just metadata + first-line-of-error if it failed.
//
// Cross-tenant by design: rows are written for system emails (password reset,
// invite, etc) which may not have a tenant context. TenantId is nullable.
public class EmailLog
{
    public int Id { get; set; }

    public DateTime At { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(320)] // RFC 5321 max email length
    public string ToAddress { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Subject { get; set; }

    /// <summary>"sent" / "failed" / "skipped" (no-op sender / disabled).</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "sent";

    [MaxLength(2000)]
    public string? Error { get; set; }

    public int? TenantId { get; set; }

    /// <summary>Optional sender-class name (SmtpEmailSender / NullEmailSender) for diagnostic.</summary>
    [MaxLength(80)]
    public string? Sender { get; set; }

    /// <summary>Approximate body size in bytes — useful for spotting outliers.</summary>
    public int? BodyBytes { get; set; }
}
