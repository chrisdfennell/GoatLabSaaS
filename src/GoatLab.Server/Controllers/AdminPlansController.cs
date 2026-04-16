using GoatLab.Server.Data;
using GoatLab.Server.Services;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Super-admin only — CRUD for subscription plans.
[ApiController]
[Route("api/admin/plans")]
[Authorize(Policy = SuperAdminPolicy.Name)]
public class AdminPlansController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly IAdminAuditLogger _audit;

    public AdminPlansController(GoatLabDbContext db, IAdminAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    public record PlanFeatureInput(AppFeature Feature, bool Enabled);

    public record PlanInput(
        string Name,
        string Slug,
        string? Description,
        int PriceMonthlyCents,
        string? StripePriceId,
        int TrialDays,
        int? MaxGoats,
        int? MaxUsers,
        bool IsPublic,
        bool IsActive,
        int DisplayOrder,
        List<PlanFeatureInput> Features);

    public record AdminPlanDto(
        int Id,
        string Name,
        string Slug,
        string? Description,
        int PriceMonthlyCents,
        string? StripePriceId,
        int TrialDays,
        int? MaxGoats,
        int? MaxUsers,
        bool IsPublic,
        bool IsActive,
        int DisplayOrder,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        int TenantCount,
        List<PlanFeatureInput> Features);

    [HttpGet]
    public async Task<ActionResult<List<AdminPlanDto>>> GetAll(CancellationToken ct)
    {
        var plans = await _db.Plans
            .OrderBy(p => p.DisplayOrder)
            .Include(p => p.Features)
            .AsNoTracking()
            .ToListAsync(ct);

        // Tenant counts in one query.
        var counts = await _db.Tenants
            .GroupBy(t => t.PlanId)
            .Select(g => new { PlanId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PlanId, x => x.Count, ct);

        return plans.Select(p => ToDto(p, counts.GetValueOrDefault(p.Id, 0))).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminPlanDto>> Get(int id, CancellationToken ct)
    {
        var plan = await _db.Plans
            .Include(p => p.Features)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return NotFound();

        var count = await _db.Tenants.CountAsync(t => t.PlanId == id, ct);
        return ToDto(plan, count);
    }

    [HttpPost]
    public async Task<ActionResult<AdminPlanDto>> Create([FromBody] PlanInput input, CancellationToken ct)
    {
        if (await _db.Plans.AnyAsync(p => p.Slug == input.Slug, ct))
            return Conflict(new { error = $"A plan with slug '{input.Slug}' already exists." });

        var plan = new Plan
        {
            Name = input.Name,
            Slug = input.Slug.ToLowerInvariant(),
            Description = input.Description,
            PriceMonthlyCents = input.PriceMonthlyCents,
            StripePriceId = string.IsNullOrWhiteSpace(input.StripePriceId) ? null : input.StripePriceId,
            TrialDays = input.TrialDays,
            MaxGoats = input.MaxGoats,
            MaxUsers = input.MaxUsers,
            IsPublic = input.IsPublic,
            IsActive = input.IsActive,
            DisplayOrder = input.DisplayOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Features = input.Features.Select(f => new PlanFeature { Feature = f.Feature, Enabled = f.Enabled }).ToList(),
        };

        _db.Plans.Add(plan);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PlanCreated", "Plan", plan.Id.ToString(), $"{plan.Name} ({plan.Slug})");

        return CreatedAtAction(nameof(Get), new { id = plan.Id }, ToDto(plan, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AdminPlanDto>> Update(int id, [FromBody] PlanInput input, CancellationToken ct)
    {
        var plan = await _db.Plans.Include(p => p.Features).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return NotFound();

        if (plan.Slug != input.Slug && await _db.Plans.AnyAsync(p => p.Slug == input.Slug && p.Id != id, ct))
            return Conflict(new { error = $"A plan with slug '{input.Slug}' already exists." });

        plan.Name = input.Name;
        plan.Slug = input.Slug.ToLowerInvariant();
        plan.Description = input.Description;
        plan.PriceMonthlyCents = input.PriceMonthlyCents;
        plan.StripePriceId = string.IsNullOrWhiteSpace(input.StripePriceId) ? null : input.StripePriceId;
        plan.TrialDays = input.TrialDays;
        plan.MaxGoats = input.MaxGoats;
        plan.MaxUsers = input.MaxUsers;
        plan.IsPublic = input.IsPublic;
        plan.IsActive = input.IsActive;
        plan.DisplayOrder = input.DisplayOrder;
        plan.UpdatedAt = DateTime.UtcNow;

        // Replace the feature set wholesale — simplest way to keep toggles in sync.
        _db.PlanFeatures.RemoveRange(plan.Features);
        plan.Features = input.Features
            .Select(f => new PlanFeature { PlanId = plan.Id, Feature = f.Feature, Enabled = f.Enabled })
            .ToList();

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PlanUpdated", "Plan", plan.Id.ToString(), $"{plan.Name} ({plan.Slug})");

        var tenantCount = await _db.Tenants.CountAsync(t => t.PlanId == id, ct);
        return ToDto(plan, tenantCount);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return NotFound();

        var tenantCount = await _db.Tenants.CountAsync(t => t.PlanId == id, ct);
        if (tenantCount > 0)
            return BadRequest(new { error = $"Cannot delete — {tenantCount} tenant(s) are on this plan. Reassign them first." });

        _db.Plans.Remove(plan);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PlanDeleted", "Plan", plan.Id.ToString(), $"{plan.Name} ({plan.Slug})");
        return NoContent();
    }

    private static AdminPlanDto ToDto(Plan p, int tenantCount) => new(
        p.Id,
        p.Name,
        p.Slug,
        p.Description,
        p.PriceMonthlyCents,
        p.StripePriceId,
        p.TrialDays,
        p.MaxGoats,
        p.MaxUsers,
        p.IsPublic,
        p.IsActive,
        p.DisplayOrder,
        p.CreatedAt,
        p.UpdatedAt,
        tenantCount,
        p.Features.Select(f => new PlanFeatureInput(f.Feature, f.Enabled)).ToList());
}
