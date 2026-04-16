using System.ComponentModel.DataAnnotations;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Identity;

namespace GoatLab.Server.Data.Auth;

public class ApplicationUser : IdentityUser
{
    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Cross-tenant admin flag. Users with this set bypass the tenant filter
    /// and can see/manage all tenants via the /admin console. Seeded from
    /// config (SuperAdmin:Emails) on startup — never exposed via register.
    /// </summary>
    public bool IsSuperAdmin { get; set; }

    /// <summary>Non-null when soft-deleted by an admin — login is rejected.</summary>
    public DateTime? DeletedAt { get; set; }

    public ICollection<TenantMember> Memberships { get; set; } = new List<TenantMember>();
}
