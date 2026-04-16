using System.Security.Claims;
using GoatLab.Server.Data;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Http;

namespace GoatLab.Server.Services;

public interface IAdminAuditLogger
{
    Task LogAsync(string action, string? targetType = null, string? targetId = null, string? detail = null);
}

/// <summary>
/// Writes <see cref="AdminAuditLog"/> rows for admin-console mutations. Resolves
/// the acting user from the current HttpContext. No-ops silently if called
/// without an authenticated user (shouldn't happen on AdminController since
/// every endpoint is policy-gated, but defensive).
/// </summary>
public class AdminAuditLogger : IAdminAuditLogger
{
    private readonly GoatLabDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ITenantContext _tenantContext;

    public AdminAuditLogger(GoatLabDbContext db, IHttpContextAccessor http, ITenantContext tenantContext)
    {
        _db = db;
        _http = http;
        _tenantContext = tenantContext;
    }

    public async Task LogAsync(string action, string? targetType = null, string? targetId = null, string? detail = null)
    {
        var user = _http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return;

        // Bypass the tenant filter — audit rows aren't tenant-owned, but
        // admin callers typically have already set this.
        _tenantContext.BypassFilter = true;

        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            ActorUserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            ActorEmail = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Detail = Truncate(detail, 1000),
        });
        await _db.SaveChangesAsync();
    }

    private static string? Truncate(string? s, int max)
        => s is null ? null : s.Length <= max ? s : s[..max];
}
