using GoatLab.Server.Data;
using GoatLab.Server.Services;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Public plan listing for the landing page and the in-app billing view.
// Admin CRUD lives in AdminPlansController.
[ApiController]
[Route("api/plans")]
public class PlansController : ControllerBase
{
    private readonly GoatLabDbContext _db;

    public PlansController(GoatLabDbContext db) => _db = db;

    public record PlanFeatureDto(string Feature, bool Enabled);

    public record PublicPlanDto(
        int Id,
        string Name,
        string Slug,
        string? Description,
        int PriceMonthlyCents,
        int TrialDays,
        int? MaxGoats,
        int? MaxUsers,
        int DisplayOrder,
        IReadOnlyList<PlanFeatureDto> Features);

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<ActionResult<List<PublicPlanDto>>> GetPublic(CancellationToken ct)
    {
        var plans = await _db.Plans
            .Where(p => p.IsPublic && p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .Include(p => p.Features)
            .AsNoTracking()
            .ToListAsync(ct);

        return plans.Select(ToDto).ToList();
    }

    private static PublicPlanDto ToDto(Plan p) => new(
        p.Id,
        p.Name,
        p.Slug,
        p.Description,
        p.PriceMonthlyCents,
        p.TrialDays,
        p.MaxGoats,
        p.MaxUsers,
        p.DisplayOrder,
        p.Features
            .Where(f => f.Enabled)
            .Select(f => new PlanFeatureDto(f.Feature.ToString(), f.Enabled))
            .ToList());
}
