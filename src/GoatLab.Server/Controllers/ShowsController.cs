using GoatLab.Server.Data;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShowsController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public ShowsController(GoatLabDbContext db) => _db = db;

    // --- Show Records ---

    [HttpGet]
    public async Task<ActionResult<List<ShowRecord>>> GetShows([FromQuery] int? goatId)
    {
        var query = _db.ShowRecords.Include(s => s.Goat).AsQueryable();
        if (goatId.HasValue) query = query.Where(s => s.GoatId == goatId.Value);
        return await query.OrderByDescending(s => s.ShowDate).ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<ShowRecord>> CreateShow(ShowRecord record)
    {
        record.CreatedAt = DateTime.UtcNow;
        _db.ShowRecords.Add(record);
        await _db.SaveChangesAsync();
        return Ok(record);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateShow(int id, ShowRecord record)
    {
        if (id != record.Id) return BadRequest();
        var existing = await _db.ShowRecords.FindAsync(id);
        if (existing is null) return NotFound();

        existing.ShowDate = record.ShowDate;
        existing.ShowName = record.ShowName;
        existing.Location = record.Location;
        existing.Class = record.Class;
        existing.Placing = record.Placing;
        existing.ClassSize = record.ClassSize;
        existing.Awards = record.Awards;
        existing.Judge = record.Judge;
        existing.Notes = record.Notes;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteShow(int id)
    {
        var record = await _db.ShowRecords.FindAsync(id);
        if (record is null) return NotFound();
        _db.ShowRecords.Remove(record);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Linear Appraisals ---

    [HttpGet("appraisals")]
    public async Task<ActionResult<List<LinearAppraisal>>> GetAppraisals([FromQuery] int? goatId)
    {
        var query = _db.LinearAppraisals.Include(l => l.Goat).AsQueryable();
        if (goatId.HasValue) query = query.Where(l => l.GoatId == goatId.Value);
        return await query.OrderByDescending(l => l.AppraisalDate).ToListAsync();
    }

    [HttpPost("appraisals")]
    public async Task<ActionResult<LinearAppraisal>> CreateAppraisal(LinearAppraisal record)
    {
        record.CreatedAt = DateTime.UtcNow;
        _db.LinearAppraisals.Add(record);
        await _db.SaveChangesAsync();
        return Ok(record);
    }

    [HttpPut("appraisals/{id}")]
    public async Task<IActionResult> UpdateAppraisal(int id, LinearAppraisal record)
    {
        if (id != record.Id) return BadRequest();
        var existing = await _db.LinearAppraisals.FindAsync(id);
        if (existing is null) return NotFound();

        existing.AppraisalDate = record.AppraisalDate;
        existing.Appraiser = record.Appraiser;
        existing.GeneralAppearance = record.GeneralAppearance;
        existing.DairyCharacter = record.DairyCharacter;
        existing.BodyCapacity = record.BodyCapacity;
        existing.MammarySystem = record.MammarySystem;
        existing.FinalScore = record.FinalScore;
        existing.Classification = record.Classification;
        existing.Notes = record.Notes;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("appraisals/{id}")]
    public async Task<IActionResult> DeleteAppraisal(int id)
    {
        var record = await _db.LinearAppraisals.FindAsync(id);
        if (record is null) return NotFound();
        _db.LinearAppraisals.Remove(record);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
