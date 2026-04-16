using System.Security.Claims;
using GoatLab.Server.Controllers;
using GoatLab.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services;

/// <summary>
/// When maintenance mode is on, block every non-GET request except admin
/// endpoints (so an admin can still flip the switch off) and the /api/account
/// logout path. GET requests continue to work so users can see state. Super
/// admins are exempt — they can write through maintenance.
/// </summary>
public class MaintenanceModeMiddleware
{
    private readonly RequestDelegate _next;

    public MaintenanceModeMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, GoatLabDbContext db, ITenantContext tenantContext)
    {
        var path = ctx.Request.Path.Value ?? string.Empty;
        var method = ctx.Request.Method;

        // Only gate API writes; static files, Blazor bundles, /admin/* all pass.
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
            HttpMethods.IsGet(method) || HttpMethods.IsHead(method) ||
            path.StartsWith("/api/admin/", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/account/logout", StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        // Super admins bypass — they need to keep working during maintenance.
        if (ctx.User?.HasClaim(SuperAdminPolicy.ClaimType, "true") == true)
        {
            await _next(ctx);
            return;
        }

        tenantContext.BypassFilter = true;
        var enabled = await AdminController.IsMaintenanceEnabledAsync(db);
        if (!enabled)
        {
            await _next(ctx);
            return;
        }

        ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        ctx.Response.Headers.RetryAfter = "60";
        await ctx.Response.WriteAsJsonAsync(new
        {
            error = "GoatLab is in maintenance mode. Please try again shortly.",
            maintenance = true,
        });
    }
}
