namespace GoatLab.Server.Services;

/// <summary>
/// Resolves the current request's tenant for EF global query filters and
/// for stamping TenantId on writes. Implementations are scoped per request.
///
/// Returns null when the request is unauthenticated or the user hasn't
/// chosen a tenant yet (e.g. during signup). In that case query filters
/// match no rows, which is the safe default.
/// </summary>
public interface ITenantContext
{
    int? TenantId { get; }

    /// <summary>
    /// Bypass the tenant filter for cross-tenant admin operations (e.g. signup
    /// creating a new tenant, super-admin console). Use sparingly.
    /// </summary>
    bool BypassFilter { get; set; }
}

public class TenantContext : ITenantContext
{
    public int? TenantId { get; set; }
    public bool BypassFilter { get; set; }
}
