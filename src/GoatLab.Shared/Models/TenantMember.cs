using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class TenantMember
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public TenantRole Role { get; set; } = TenantRole.Owner;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public enum TenantRole
{
    Owner = 0,
    Manager = 1,
    Worker = 2,
    ReadOnly = 3
}
