using System.ComponentModel.DataAnnotations;
using GoatLab.Shared.Models;

namespace GoatLab.Shared.DTOs;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required, MaxLength(100)] string DisplayName,
    [Required, MaxLength(100)] string FarmName
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    bool RememberMe = false
);

public record SelectTenantRequest([Required] int TenantId);

public record CurrentUserDto(
    string Id,
    string Email,
    string DisplayName,
    int? CurrentTenantId,
    IReadOnlyList<TenantMembershipDto> Memberships,
    bool IsSuperAdmin = false
);

public record TenantMembershipDto(
    int TenantId,
    string Name,
    string Slug,
    TenantRole Role
);
