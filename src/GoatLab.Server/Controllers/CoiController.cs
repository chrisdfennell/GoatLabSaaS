using GoatLab.Server.Services.Pedigree;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequiresFeature(AppFeature.CoiCalculator)]
public class CoiController : ControllerBase
{
    private readonly CoiCalculator _coi;

    public CoiController(CoiCalculator coi) => _coi = coi;

    // Wright's coefficient of inbreeding for the goat using its own SireId/DamId.
    [HttpGet("{goatId:int}")]
    public async Task<ActionResult<CoiResult>> Get(int goatId, CancellationToken ct)
    {
        var result = await _coi.ComputeAsync(goatId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    // Hypothetical COI of an offspring of the named sire and dam.
    [HttpGet("{sireId:int}/with/{damId:int}")]
    public async Task<ActionResult<CoiResult>> Mate(int sireId, int damId, CancellationToken ct)
    {
        var result = await _coi.ComputeForMateAsync(sireId, damId, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
