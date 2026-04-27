using GoatLab.Server.Data;
using GoatLab.Server.Services;
using GoatLab.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Cross-tenant lookup for support triage. Searches goats, customers, tenants,
// users, sales, and transactions across every tenant — `IgnoreQueryFilters`
// since super-admin has no tenant claim. Per-category cap keeps result sets
// bounded; `Truncated=true` when any single category hit the cap.
[ApiController]
[Route("api/admin/search")]
[Authorize(Policy = SuperAdminPolicy.Name)]
public class AdminSearchController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public AdminSearchController(GoatLabDbContext db) => _db = db;

    [HttpGet]
    public async Task<AdminSearchResponse> Search([FromQuery] string q, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return new AdminSearchResponse(q ?? "", Array.Empty<AdminSearchHit>(), 0, false);

        var query = q.Trim();
        var perCategoryCap = Math.Clamp(limit, 5, 50);
        var hits = new List<AdminSearchHit>(perCategoryCap * 6);
        var truncated = false;

        // ----- Tenants -----
        var tenants = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.Name.Contains(query) || t.Slug.Contains(query))
            .OrderBy(t => t.Name)
            .Take(perCategoryCap + 1)
            .Select(t => new { t.Id, t.Name, t.Slug, t.Location, t.SubscriptionStatus, t.DeletedAt })
            .ToListAsync(ct);
        if (tenants.Count > perCategoryCap) { truncated = true; tenants = tenants.Take(perCategoryCap).ToList(); }
        foreach (var t in tenants)
            hits.Add(new AdminSearchHit("tenant", t.Name,
                $"{t.Slug}{(t.Location is null ? "" : $" · {t.Location}")} · {t.SubscriptionStatus ?? "no-sub"}{(t.DeletedAt.HasValue ? " · DELETED" : "")}",
                $"/admin/tenants/{t.Id}", t.Slug, t.Name));

        // ----- Users -----
        var users = await _db.Users
            .Where(u => (u.Email != null && u.Email.Contains(query))
                     || (u.DisplayName != null && u.DisplayName.Contains(query)))
            .OrderBy(u => u.Email)
            .Take(perCategoryCap + 1)
            .Select(u => new { u.Id, u.Email, u.DisplayName, u.IsSuperAdmin })
            .ToListAsync(ct);
        if (users.Count > perCategoryCap) { truncated = true; users = users.Take(perCategoryCap).ToList(); }
        foreach (var u in users)
        {
            hits.Add(new AdminSearchHit("user",
                string.IsNullOrEmpty(u.DisplayName) ? (u.Email ?? "(no email)") : u.DisplayName,
                $"{u.Email}{(u.IsSuperAdmin ? " · super-admin" : "")}",
                $"/admin/users/{u.Id}", null, null));
        }

        // ----- Goats -----
        var goats = await _db.Goats.IgnoreQueryFilters()
            .Where(g => g.Name.Contains(query)
                     || (g.EarTag != null && g.EarTag.Contains(query))
                     || (g.RegistrationNumber != null && g.RegistrationNumber.Contains(query)))
            .OrderBy(g => g.Name)
            .Take(perCategoryCap + 1)
            .Select(g => new
            {
                g.Id, g.Name, g.EarTag, g.RegistrationNumber, g.Breed,
                TenantSlug = g.Tenant!.Slug,
                TenantName = g.Tenant.Name,
            })
            .ToListAsync(ct);
        if (goats.Count > perCategoryCap) { truncated = true; goats = goats.Take(perCategoryCap).ToList(); }
        foreach (var g in goats)
        {
            var idBits = string.Join(" / ", new[] { g.EarTag, g.RegistrationNumber }
                .Where(s => !string.IsNullOrEmpty(s)));
            hits.Add(new AdminSearchHit("goat", g.Name,
                $"{g.Breed ?? "?"}{(string.IsNullOrEmpty(idBits) ? "" : $" · {idBits}")} · {g.TenantName}",
                $"/herd/{g.Id}", g.TenantSlug, g.TenantName));
        }

        // ----- Customers -----
        var customers = await _db.Customers.IgnoreQueryFilters()
            .Where(c => c.Name.Contains(query) || (c.Email != null && c.Email.Contains(query)))
            .OrderBy(c => c.Name)
            .Take(perCategoryCap + 1)
            .Select(c => new
            {
                c.Id, c.Name, c.Email, c.Phone,
                TenantSlug = c.Tenant!.Slug,
                TenantName = c.Tenant.Name,
            })
            .ToListAsync(ct);
        if (customers.Count > perCategoryCap) { truncated = true; customers = customers.Take(perCategoryCap).ToList(); }
        foreach (var c in customers)
            hits.Add(new AdminSearchHit("customer", c.Name,
                $"{c.Email ?? c.Phone ?? "(no contact)"} · {c.TenantName}",
                "/sales/customers", c.TenantSlug, c.TenantName));

        // ----- Sales (search by id when query is numeric) -----
        if (int.TryParse(query, out var saleId))
        {
            var sale = await _db.Sales.IgnoreQueryFilters()
                .Where(s => s.Id == saleId)
                .Select(s => new
                {
                    s.Id, s.SaleDate, s.Amount, s.SaleType,
                    TenantSlug = s.Tenant!.Slug,
                    TenantName = s.Tenant.Name,
                })
                .FirstOrDefaultAsync(ct);
            if (sale is not null)
                hits.Add(new AdminSearchHit("sale", $"Sale #{sale.Id}",
                    $"${sale.Amount:N2} · {sale.SaleType} · {sale.SaleDate:yyyy-MM-dd} · {sale.TenantName}",
                    "/sales", sale.TenantSlug, sale.TenantName));
        }

        return new AdminSearchResponse(query, hits, hits.Count, truncated);
    }
}
