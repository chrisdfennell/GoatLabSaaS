using GoatLab.Server.Data;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MilkController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public MilkController(GoatLabDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<MilkLog>>> GetAll([FromQuery] int? goatId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var query = _db.MilkLogs.Include(m => m.Goat).AsQueryable();

        if (goatId.HasValue) query = query.Where(m => m.GoatId == goatId.Value);
        if (from.HasValue) query = query.Where(m => m.Date >= from.Value);
        if (to.HasValue) query = query.Where(m => m.Date <= to.Value);

        return await query.OrderByDescending(m => m.Date).ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<MilkLog>> Create(MilkLog log)
    {
        log.CreatedAt = DateTime.UtcNow;
        _db.MilkLogs.Add(log);
        await _db.SaveChangesAsync();
        return Ok(log);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, MilkLog log)
    {
        if (id != log.Id) return BadRequest();
        var existing = await _db.MilkLogs.FindAsync(id);
        if (existing is null) return NotFound();

        existing.GoatId = log.GoatId;
        existing.Date = log.Date;
        existing.Amount = log.Amount;
        existing.Notes = log.Notes;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var log = await _db.MilkLogs.FindAsync(id);
        if (log is null) return NotFound();
        _db.MilkLogs.Remove(log);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("trends")]
    public async Task<ActionResult<object>> GetTrends([FromQuery] int? goatId, [FromQuery] int days = 30)
    {
        var from = DateTime.UtcNow.AddDays(-days);
        var query = _db.MilkLogs.Where(m => m.Date >= from);
        if (goatId.HasValue) query = query.Where(m => m.GoatId == goatId.Value);

        var daily = await query
            .GroupBy(m => m.Date.Date)
            .Select(g => new { date = g.Key, totalLbs = g.Sum(m => m.Amount), entries = g.Count() })
            .OrderBy(d => d.date)
            .ToListAsync();

        return Ok(daily);
    }

    // --- Lactations ---

    [HttpGet("lactations")]
    public async Task<ActionResult<List<object>>> GetLactations([FromQuery] int? goatId)
    {
        var query = _db.Lactations
            .Include(l => l.Goat)
            .Include(l => l.TestDays)
            .AsQueryable();
        if (goatId.HasValue) query = query.Where(l => l.GoatId == goatId.Value);

        var lactations = await query.OrderByDescending(l => l.FreshenDate).ToListAsync();
        var results = new List<object>();
        foreach (var l in lactations)
        {
            results.Add(await BuildLactationSummaryAsync(l));
        }
        return Ok(results);
    }

    [HttpGet("lactations/{id}")]
    public async Task<ActionResult<object>> GetLactation(int id)
    {
        var lactation = await _db.Lactations
            .Include(l => l.Goat)
            .Include(l => l.TestDays)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (lactation is null) return NotFound();
        return Ok(await BuildLactationSummaryAsync(lactation));
    }

    [HttpPost("lactations")]
    public async Task<ActionResult<Lactation>> CreateLactation(Lactation lactation)
    {
        if (lactation.LactationNumber <= 0)
        {
            var existing = await _db.Lactations.CountAsync(l => l.GoatId == lactation.GoatId);
            lactation.LactationNumber = existing + 1;
        }
        lactation.CreatedAt = DateTime.UtcNow;
        _db.Lactations.Add(lactation);
        await _db.SaveChangesAsync();
        return Ok(lactation);
    }

    [HttpPut("lactations/{id}")]
    public async Task<IActionResult> UpdateLactation(int id, Lactation lactation)
    {
        if (id != lactation.Id) return BadRequest();
        var existing = await _db.Lactations.FindAsync(id);
        if (existing is null) return NotFound();

        existing.FreshenDate = lactation.FreshenDate;
        existing.DryOffDate = lactation.DryOffDate;
        existing.LactationNumber = lactation.LactationNumber;
        existing.KiddingRecordId = lactation.KiddingRecordId;
        existing.Notes = lactation.Notes;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("lactations/{id}")]
    public async Task<IActionResult> DeleteLactation(int id)
    {
        var lactation = await _db.Lactations.FindAsync(id);
        if (lactation is null) return NotFound();
        _db.Lactations.Remove(lactation);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Test Days ---

    [HttpGet("lactations/{lactationId}/testdays")]
    public async Task<ActionResult<List<MilkTestDay>>> GetTestDays(int lactationId)
    {
        return await _db.MilkTestDays
            .Where(t => t.LactationId == lactationId)
            .OrderByDescending(t => t.TestDate)
            .ToListAsync();
    }

    [HttpPost("testdays")]
    public async Task<ActionResult<MilkTestDay>> CreateTestDay(MilkTestDay test)
    {
        if (test.TotalLbs <= 0 && (test.AmLbs.HasValue || test.PmLbs.HasValue))
            test.TotalLbs = (test.AmLbs ?? 0) + (test.PmLbs ?? 0);

        // Ensure GoatId is set from lactation if not provided
        if (test.GoatId == 0)
        {
            var lactation = await _db.Lactations.FindAsync(test.LactationId);
            if (lactation != null) test.GoatId = lactation.GoatId;
        }

        test.CreatedAt = DateTime.UtcNow;
        _db.MilkTestDays.Add(test);
        await _db.SaveChangesAsync();
        return Ok(test);
    }

    [HttpPut("testdays/{id}")]
    public async Task<IActionResult> UpdateTestDay(int id, MilkTestDay test)
    {
        if (id != test.Id) return BadRequest();
        var existing = await _db.MilkTestDays.FindAsync(id);
        if (existing is null) return NotFound();

        existing.TestDate = test.TestDate;
        existing.AmLbs = test.AmLbs;
        existing.PmLbs = test.PmLbs;
        existing.TotalLbs = test.TotalLbs > 0 ? test.TotalLbs : (test.AmLbs ?? 0) + (test.PmLbs ?? 0);
        existing.ButterfatPercent = test.ButterfatPercent;
        existing.ProteinPercent = test.ProteinPercent;
        existing.SomaticCellCount = test.SomaticCellCount;
        existing.Notes = test.Notes;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("testdays/{id}")]
    public async Task<IActionResult> DeleteTestDay(int id)
    {
        var test = await _db.MilkTestDays.FindAsync(id);
        if (test is null) return NotFound();
        _db.MilkTestDays.Remove(test);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Lactation rollup helper ---

    private async Task<object> BuildLactationSummaryAsync(Lactation lactation)
    {
        var end = lactation.DryOffDate ?? DateTime.UtcNow;
        var dim = Math.Max(0, (int)((end - lactation.FreshenDate).TotalDays));
        var isActive = lactation.DryOffDate == null;

        // Daily logs that fall within this lactation window
        var dailyLogs = await _db.MilkLogs
            .Where(m => m.GoatId == lactation.GoatId
                        && m.Date >= lactation.FreshenDate
                        && (lactation.DryOffDate == null || m.Date <= lactation.DryOffDate))
            .OrderBy(m => m.Date)
            .ToListAsync();

        var dailyTotals = dailyLogs
            .GroupBy(m => m.Date.Date)
            .Select(g => new { date = g.Key, lbs = g.Sum(m => m.Amount) })
            .OrderBy(d => d.date)
            .ToList();

        double totalMilk = dailyTotals.Sum(d => d.lbs);
        double peakLbs = dailyTotals.Any() ? dailyTotals.Max(d => d.lbs) : 0;
        DateTime? peakDate = dailyTotals.Any() ? dailyTotals.First(d => d.lbs == peakLbs).date : null;
        int peakDim = peakDate.HasValue ? (int)(peakDate.Value - lactation.FreshenDate).TotalDays : 0;
        double avgDaily = dailyTotals.Any() ? totalMilk / dailyTotals.Count : 0;

        // Crude 305-day projection: current daily average × 305. Only meaningful if DIM >= 14.
        double? projected305 = dim >= 14 && avgDaily > 0 ? Math.Round(avgDaily * 305, 1) : null;

        return new
        {
            id = lactation.Id,
            goatId = lactation.GoatId,
            goatName = lactation.Goat?.Name,
            lactationNumber = lactation.LactationNumber,
            freshenDate = lactation.FreshenDate,
            dryOffDate = lactation.DryOffDate,
            kiddingRecordId = lactation.KiddingRecordId,
            notes = lactation.Notes,
            isActive,
            daysInMilk = dim,
            totalMilkLbs = Math.Round(totalMilk, 1),
            avgDailyLbs = Math.Round(avgDaily, 2),
            peakLbs = Math.Round(peakLbs, 1),
            peakDim,
            projected305,
            daysWithData = dailyTotals.Count,
            testDays = lactation.TestDays?
                .OrderByDescending(t => t.TestDate)
                .Select(t => new
                {
                    t.Id, t.TestDate, t.AmLbs, t.PmLbs, t.TotalLbs,
                    t.ButterfatPercent, t.ProteinPercent, t.SomaticCellCount, t.Notes,
                    dim = (int)(t.TestDate - lactation.FreshenDate).TotalDays
                }).ToList()
        };
    }
}
