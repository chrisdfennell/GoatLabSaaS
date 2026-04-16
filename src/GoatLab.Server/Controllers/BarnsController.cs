using GoatLab.Server.Data;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequiresFeature(AppFeature.Barns)]
public class BarnsController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public BarnsController(GoatLabDbContext db) => _db = db;

    // --- Barns ---

    [HttpGet]
    public async Task<ActionResult<List<Barn>>> GetAll()
    {
        return await _db.Barns
            .Include(b => b.Pens).ThenInclude(p => p.Goats)
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Barn>> Get(int id)
    {
        var barn = await _db.Barns
            .Include(b => b.Pens).ThenInclude(p => p.Goats)
            .FirstOrDefaultAsync(b => b.Id == id);
        return barn is null ? NotFound() : barn;
    }

    [HttpPost]
    public async Task<ActionResult<Barn>> Create(Barn barn)
    {
        _db.Barns.Add(barn);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = barn.Id }, barn);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Barn barn)
    {
        if (id != barn.Id) return BadRequest();
        var existing = await _db.Barns.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = barn.Name;
        existing.Description = barn.Description;
        existing.Latitude = barn.Latitude;
        existing.Longitude = barn.Longitude;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id}/position")]
    public async Task<IActionResult> SetPosition(int id, [FromBody] BarnPosition pos)
    {
        var existing = await _db.Barns.FindAsync(id);
        if (existing is null) return NotFound();
        existing.Latitude = pos.Latitude;
        existing.Longitude = pos.Longitude;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public record BarnPosition(double Latitude, double Longitude);

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var barn = await _db.Barns.FindAsync(id);
        if (barn is null) return NotFound();
        _db.Barns.Remove(barn);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Pens ---

    [HttpGet("{barnId}/pens")]
    public async Task<ActionResult<List<Pen>>> GetPens(int barnId)
    {
        return await _db.Pens
            .Where(p => p.BarnId == barnId)
            .Include(p => p.Goats)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    [HttpPost("{barnId}/pens")]
    public async Task<ActionResult<Pen>> CreatePen(int barnId, Pen pen)
    {
        pen.BarnId = barnId;
        _db.Pens.Add(pen);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = barnId }, pen);
    }

    [HttpPut("pens/{penId}")]
    public async Task<IActionResult> UpdatePen(int penId, Pen pen)
    {
        if (penId != pen.Id) return BadRequest();
        var existing = await _db.Pens.FindAsync(penId);
        if (existing is null) return NotFound();

        existing.Name = pen.Name;
        existing.Capacity = pen.Capacity;
        existing.Notes = pen.Notes;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("pens/{penId}")]
    public async Task<IActionResult> DeletePen(int penId)
    {
        var pen = await _db.Pens.FindAsync(penId);
        if (pen is null) return NotFound();
        _db.Pens.Remove(pen);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
