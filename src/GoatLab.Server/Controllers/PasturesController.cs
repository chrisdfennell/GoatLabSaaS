using GoatLab.Server.Data;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequiresFeature(AppFeature.Map)]
public class PasturesController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public PasturesController(GoatLabDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<Pasture>>> GetAll()
    {
        return await _db.Pastures
            .Include(p => p.ConditionLogs.OrderByDescending(c => c.Date).Take(1))
            .Include(p => p.Rotations.Where(r => r.EndDate == null))
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Pasture>> Get(int id)
    {
        var pasture = await _db.Pastures
            .Include(p => p.ConditionLogs.OrderByDescending(c => c.Date))
            .Include(p => p.Rotations.OrderByDescending(r => r.StartDate))
            .FirstOrDefaultAsync(p => p.Id == id);
        return pasture is null ? NotFound() : pasture;
    }

    [HttpPost]
    public async Task<ActionResult<Pasture>> Create(Pasture pasture)
    {
        pasture.CreatedAt = DateTime.UtcNow;
        pasture.UpdatedAt = DateTime.UtcNow;
        _db.Pastures.Add(pasture);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = pasture.Id }, pasture);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Pasture pasture)
    {
        if (id != pasture.Id) return BadRequest();
        var existing = await _db.Pastures.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = pasture.Name;
        existing.GeoJson = pasture.GeoJson;
        existing.Acreage = pasture.Acreage;
        existing.PerimeterFeet = pasture.PerimeterFeet;
        existing.Condition = pasture.Condition;
        existing.StockingCapacity = pasture.StockingCapacity;
        existing.Notes = pasture.Notes;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var pasture = await _db.Pastures.FindAsync(id);
        if (pasture is null) return NotFound();
        _db.Pastures.Remove(pasture);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Condition Logs ---

    [HttpGet("{pastureId}/conditions")]
    public async Task<ActionResult<List<PastureConditionLog>>> GetConditions(int pastureId)
    {
        return await _db.PastureConditionLogs
            .Where(c => c.PastureId == pastureId)
            .OrderByDescending(c => c.Date)
            .ToListAsync();
    }

    [HttpPost("{pastureId}/conditions")]
    public async Task<ActionResult<PastureConditionLog>> CreateCondition(int pastureId, PastureConditionLog log)
    {
        log.PastureId = pastureId;
        _db.PastureConditionLogs.Add(log);

        // Update the pasture's current condition
        var pasture = await _db.Pastures.FindAsync(pastureId);
        if (pasture != null)
        {
            pasture.Condition = log.Condition;
            pasture.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(log);
    }

    // --- Rotations ---

    [HttpGet("{pastureId}/rotations")]
    public async Task<ActionResult<List<PastureRotation>>> GetRotations(int pastureId)
    {
        return await _db.PastureRotations
            .Where(r => r.PastureId == pastureId)
            .OrderByDescending(r => r.StartDate)
            .ToListAsync();
    }

    [HttpPost("{pastureId}/rotations")]
    public async Task<ActionResult<PastureRotation>> CreateRotation(int pastureId, PastureRotation rotation)
    {
        rotation.PastureId = pastureId;
        _db.PastureRotations.Add(rotation);
        await _db.SaveChangesAsync();
        return Ok(rotation);
    }

    [HttpPut("rotations/{id}/end")]
    public async Task<IActionResult> EndRotation(int id)
    {
        var rotation = await _db.PastureRotations.FindAsync(id);
        if (rotation is null) return NotFound();
        rotation.EndDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Rotation Timeline (90 days) ---

    [HttpGet("rotation-timeline")]
    public async Task<ActionResult<object>> GetRotationTimeline()
    {
        var from = DateTime.UtcNow.AddDays(-90);
        var rotations = await _db.PastureRotations
            .Include(r => r.Pasture)
            .Where(r => r.StartDate >= from || (r.EndDate == null && r.StartDate < from))
            .OrderBy(r => r.StartDate)
            .ToListAsync();

        return Ok(rotations);
    }

    // --- Stocking Rate Calculator ---

    [HttpGet("stocking-rate")]
    public ActionResult<object> CalculateStockingRate([FromQuery] double acreage, [FromQuery] double goatsPerAcre = 6)
    {
        var capacity = (int)(acreage * goatsPerAcre);
        return Ok(new { acreage, goatsPerAcre, recommendedCapacity = capacity });
    }
}
