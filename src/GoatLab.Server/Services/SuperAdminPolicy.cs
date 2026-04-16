namespace GoatLab.Server.Services;

/// <summary>
/// Authorization policy marker for cross-tenant admin endpoints.
/// The <see cref="ClaimType"/> is emitted by AccountController at sign-in
/// when ApplicationUser.IsSuperAdmin is true, and consumed by
/// [Authorize(Policy = SuperAdminPolicy.Name)] on AdminController.
/// </summary>
public static class SuperAdminPolicy
{
    public const string Name = "SuperAdmin";
    public const string ClaimType = "super_admin";
}
