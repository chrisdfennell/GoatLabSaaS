using GoatLab.Server.Data;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequiresFeature(AppFeature.Breeding)]
public class BreedingController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public BreedingController(GoatLabDbContext db) => _db = db;

    // --- Breeding Records ---

    [HttpGet]
    public async Task<ActionResult<List<BreedingRecord>>> GetAll([FromQuery] BreedingOutcome? outcome)
    {
        var query = _db.BreedingRecords
            .Include(b => b.Doe)
            .Include(b => b.Buck)
            .Include(b => b.KiddingRecords)
            .AsQueryable();

        if (outcome.HasValue) query = query.Where(b => b.Outcome == outcome.Value);

        return await query.OrderByDescending(b => b.BreedingDate).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BreedingRecord>> Get(int id)
    {
        var record = await _db.BreedingRecords
            .Include(b => b.Doe)
            .Include(b => b.Buck)
            .Include(b => b.KiddingRecords).ThenInclude(k => k.KidGoat)
            .FirstOrDefaultAsync(b => b.Id == id);
        return record is null ? NotFound() : record;
    }

    [HttpPost]
    public async Task<ActionResult<BreedingRecord>> Create(BreedingRecord record)
    {
        // Auto-calculate estimated due date (~150 days gestation)
        record.EstimatedDueDate ??= record.BreedingDate.AddDays(150);
        record.CreatedAt = DateTime.UtcNow;

        _db.BreedingRecords.Add(record);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = record.Id }, record);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, BreedingRecord record)
    {
        if (id != record.Id) return BadRequest();
        var existing = await _db.BreedingRecords.FindAsync(id);
        if (existing is null) return NotFound();

        existing.DoeId = record.DoeId;
        existing.BuckId = record.BuckId;
        existing.BreedingDate = record.BreedingDate;
        existing.EstimatedDueDate = record.EstimatedDueDate ?? record.BreedingDate.AddDays(150);
        existing.Outcome = record.Outcome;
        existing.Notes = record.Notes;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await _db.BreedingRecords.FindAsync(id);
        if (record is null) return NotFound();
        _db.BreedingRecords.Remove(record);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Kidding Records ---

    [HttpGet("{breedingId}/kidding")]
    public async Task<ActionResult<List<KiddingRecord>>> GetKiddingRecords(int breedingId)
    {
        return await _db.KiddingRecords
            .Where(k => k.BreedingRecordId == breedingId)
            .Include(k => k.KidGoat)
            .Include(k => k.Kids).ThenInclude(kid => kid.LinkedGoat)
            .OrderByDescending(k => k.KiddingDate)
            .ToListAsync();
    }

    [HttpGet("kidding/{id}")]
    public async Task<ActionResult<KiddingRecord>> GetKiddingRecord(int id)
    {
        var kr = await _db.KiddingRecords
            .Include(k => k.BreedingRecord).ThenInclude(b => b.Doe)
            .Include(k => k.BreedingRecord).ThenInclude(b => b.Buck)
            .Include(k => k.Kids).ThenInclude(kid => kid.LinkedGoat)
            .FirstOrDefaultAsync(k => k.Id == id);
        return kr is null ? NotFound() : kr;
    }

    [HttpPost("{breedingId}/kidding")]
    public async Task<ActionResult<KiddingRecord>> CreateKiddingRecord(int breedingId, KiddingRecord record)
    {
        record.BreedingRecordId = breedingId;
        record.CreatedAt = DateTime.UtcNow;
        // Kids collection in the payload is persisted via EF's graph insert
        _db.KiddingRecords.Add(record);

        // Mark breeding as confirmed when a kidding is recorded
        var breeding = await _db.BreedingRecords.FindAsync(breedingId);
        if (breeding != null && breeding.Outcome != BreedingOutcome.Confirmed)
            breeding.Outcome = BreedingOutcome.Confirmed;

        await _db.SaveChangesAsync();

        // Auto-start a lactation for the dam if this kidding was healthy and she doesn't
        // already have an active lactation that started within 2 days of this kidding.
        if (breeding != null && record.Outcome is KiddingOutcome.Healthy or KiddingOutcome.Complications)
        {
            var window = record.KiddingDate.AddDays(-2);
            var hasOverlap = await _db.Lactations.AnyAsync(l =>
                l.GoatId == breeding.DoeId && l.FreshenDate >= window && l.DryOffDate == null);
            if (!hasOverlap)
            {
                var number = await _db.Lactations.CountAsync(l => l.GoatId == breeding.DoeId) + 1;
                _db.Lactations.Add(new Lactation
                {
                    GoatId = breeding.DoeId,
                    FreshenDate = record.KiddingDate,
                    KiddingRecordId = record.Id,
                    LactationNumber = number,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }
        }
        return Ok(record);
    }

    [HttpPut("kidding/{id}")]
    public async Task<IActionResult> UpdateKiddingRecord(int id, KiddingRecord record)
    {
        if (id != record.Id) return BadRequest();
        var existing = await _db.KiddingRecords.FindAsync(id);
        if (existing is null) return NotFound();

        existing.KiddingDate = record.KiddingDate;
        existing.KidsBorn = record.KidsBorn;
        existing.KidsAlive = record.KidsAlive;
        existing.Outcome = record.Outcome;
        existing.DifficultyScore = record.DifficultyScore;
        existing.AssistanceGiven = record.AssistanceGiven;
        existing.ColostrumGiven = record.ColostrumGiven;
        existing.DamStatus = record.DamStatus;
        existing.KidGoatId = record.KidGoatId;
        existing.Complications = record.Complications;
        existing.Notes = record.Notes;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Individual Kids on a KiddingRecord ---

    [HttpPost("kidding/{kiddingId}/kids")]
    public async Task<ActionResult<Kid>> AddKid(int kiddingId, Kid kid)
    {
        var kidding = await _db.KiddingRecords.FindAsync(kiddingId);
        if (kidding is null) return NotFound();

        kid.KiddingRecordId = kiddingId;
        kid.CreatedAt = DateTime.UtcNow;
        _db.Kids.Add(kid);
        await _db.SaveChangesAsync();
        return Ok(kid);
    }

    [HttpPut("kids/{id}")]
    public async Task<IActionResult> UpdateKid(int id, Kid kid)
    {
        if (id != kid.Id) return BadRequest();
        var existing = await _db.Kids.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = kid.Name;
        existing.Gender = kid.Gender;
        existing.BirthWeightLbs = kid.BirthWeightLbs;
        existing.Presentation = kid.Presentation;
        existing.Vigor = kid.Vigor;
        existing.LinkedGoatId = kid.LinkedGoatId;
        existing.Notes = kid.Notes;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("kids/{id}")]
    public async Task<IActionResult> DeleteKid(int id)
    {
        var kid = await _db.Kids.FindAsync(id);
        if (kid is null) return NotFound();
        _db.Kids.Remove(kid);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Promote a kid to a full Goat record in the herd (sets pedigree from the breeding parents).</summary>
    [HttpPost("kids/{id}/promote")]
    public async Task<ActionResult<Goat>> PromoteKid(int id)
    {
        var kid = await _db.Kids
            .Include(k => k.KiddingRecord).ThenInclude(kr => kr.BreedingRecord)
            .FirstOrDefaultAsync(k => k.Id == id);
        if (kid is null) return NotFound();
        if (kid.LinkedGoatId != null) return Conflict("Kid is already linked to a goat.");

        var breeding = kid.KiddingRecord.BreedingRecord;
        var goat = new Goat
        {
            Name = string.IsNullOrWhiteSpace(kid.Name) ? $"Kid {DateTime.UtcNow:yyyyMMdd-HHmm}" : kid.Name!,
            Gender = kid.Gender,
            DateOfBirth = kid.KiddingRecord.KiddingDate,
            Status = kid.Vigor == KidVigor.Died ? GoatStatus.Deceased : GoatStatus.Healthy,
            DamId = breeding.DoeId,
            SireId = breeding.BuckId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Goats.Add(goat);
        await _db.SaveChangesAsync();

        kid.LinkedGoatId = goat.Id;
        if (kid.BirthWeightLbs.HasValue)
        {
            _db.WeightRecords.Add(new WeightRecord
            {
                GoatId = goat.Id,
                Date = kid.KiddingRecord.KiddingDate,
                Weight = kid.BirthWeightLbs.Value,
                Notes = "Birth weight"
            });
        }
        await _db.SaveChangesAsync();

        return Ok(goat);
    }

    [HttpDelete("kidding/{id}")]
    public async Task<IActionResult> DeleteKiddingRecord(int id)
    {
        var record = await _db.KiddingRecords.FindAsync(id);
        if (record is null) return NotFound();
        _db.KiddingRecords.Remove(record);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Heat Detection ---

    [HttpGet("heat")]
    public async Task<ActionResult<List<HeatDetection>>> GetHeatDetections([FromQuery] int? goatId)
    {
        var query = _db.HeatDetections.Include(h => h.Goat).AsQueryable();
        if (goatId.HasValue) query = query.Where(h => h.GoatId == goatId.Value);
        return await query.OrderByDescending(h => h.DetectedDate).ToListAsync();
    }

    [HttpPost("heat")]
    public async Task<ActionResult<HeatDetection>> CreateHeatDetection(HeatDetection detection)
    {
        // Auto-predict next heat cycle (~21 days)
        detection.PredictedNextHeat ??= detection.DetectedDate.AddDays(21);
        _db.HeatDetections.Add(detection);
        await _db.SaveChangesAsync();
        return Ok(detection);
    }

    [HttpDelete("heat/{id}")]
    public async Task<IActionResult> DeleteHeatDetection(int id)
    {
        var detection = await _db.HeatDetections.FindAsync(id);
        if (detection is null) return NotFound();
        _db.HeatDetections.Remove(detection);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Kidding Season Dashboard ---

    [HttpGet("kidding-season")]
    public async Task<ActionResult<object>> GetKiddingSeason()
    {
        var upcoming = await _db.BreedingRecords
            .Include(b => b.Doe)
            .Where(b => b.Outcome == BreedingOutcome.Confirmed && b.EstimatedDueDate > DateTime.UtcNow)
            .OrderBy(b => b.EstimatedDueDate)
            .ToListAsync();

        var recentBirths = await _db.KiddingRecords
            .Include(k => k.BreedingRecord).ThenInclude(b => b.Doe)
            .Where(k => k.KiddingDate >= DateTime.UtcNow.AddDays(-30))
            .OrderByDescending(k => k.KiddingDate)
            .ToListAsync();

        return Ok(new { upcomingDueDates = upcoming, recentBirths });
    }

    // --- Gestation Calculator ---

    [HttpGet("gestation-calc")]
    public ActionResult<object> CalculateGestation([FromQuery] DateTime breedingDate, [FromQuery] int gestationDays = 150)
    {
        var dueDate = breedingDate.AddDays(gestationDays);
        var daysRemaining = (dueDate - DateTime.UtcNow).Days;
        return Ok(new { breedingDate, gestationDays, estimatedDueDate = dueDate, daysRemaining = Math.Max(0, daysRemaining) });
    }
}
