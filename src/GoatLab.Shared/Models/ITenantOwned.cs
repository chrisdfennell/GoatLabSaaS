namespace GoatLab.Shared.Models;

/// <summary>
/// Marks an entity as scoped to a single tenant. EF global query filters
/// automatically restrict queries to <see cref="TenantId"/> matching the
/// current request's tenant context.
/// </summary>
public interface ITenantOwned
{
    int TenantId { get; set; }
    Tenant? Tenant { get; set; }
}
