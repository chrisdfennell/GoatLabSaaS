using GoatLab.Server.Services.Pedigree;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/mate-recommendations")]
[RequiresFeature(AppFeature.CoiCalculator)]
public class MateRecommendationsController : ControllerBase
{
    private readonly MateRecommendationService _svc;

    public MateRecommendationsController(MateRecommendationService svc) => _svc = svc;

    // Ranks every intact male in the tenant as a mate for this doe. Returns up
    // to <paramref name="limit"/> rows sorted by composite score descending.
    [HttpGet("{doeId:int}")]
    public async Task<ActionResult<IReadOnlyList<MateRecommendationDto>>> Get(
        int doeId,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 50);
        var results = await _svc.RecommendForDoeAsync(doeId, limit, ct);
        return results is null ? NotFound() : Ok(results);
    }
}
