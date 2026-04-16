using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

/// <summary>
/// Cross-tenant audit record. NOT ITenantOwned — audit rows span tenants and
/// must never be filtered out by the tenant query filter. Written only from
/// the admin console via IAdminAuditLog.
/// </summary>
public class AdminAuditLog
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string ActorUserId { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string ActorEmail { get; set; } = string.Empty;

    /// <summary>Short verb-like key, e.g. "tenant.rename", "user.reset_password".</summary>
    [Required, MaxLength(64)]
    public string Action { get; set; } = string.Empty;

    /// <summary>Entity type the action targeted, e.g. "Tenant", "User", "Impersonation".</summary>
    [MaxLength(32)]
    public string? TargetType { get; set; }

    [MaxLength(128)]
    public string? TargetId { get; set; }

    /// <summary>Free-form summary, e.g. "Test → Test Farm" for a rename.</summary>
    [MaxLength(1000)]
    public string? Detail { get; set; }

    public DateTime At { get; set; } = DateTime.UtcNow;
}
