using GoatLab.Server.Data;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequiresFeature(AppFeature.Health)]
public class HealthController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public HealthController(GoatLabDbContext db) => _db = db;

    // --- Medical Records ---

    [HttpGet("records")]
    public async Task<ActionResult<List<MedicalRecord>>> GetRecords([FromQuery] int? goatId, [FromQuery] MedicalRecordType? type)
    {
        var query = _db.MedicalRecords
            .Include(r => r.Goat)
            .Include(r => r.Medication)
            .AsQueryable();

        if (goatId.HasValue) query = query.Where(r => r.GoatId == goatId.Value);
        if (type.HasValue) query = query.Where(r => r.RecordType == type.Value);

        return await query.OrderByDescending(r => r.Date).ToListAsync();
    }

    [HttpGet("records/{id}")]
    public async Task<ActionResult<MedicalRecord>> GetRecord(int id)
    {
        var record = await _db.MedicalRecords
            .Include(r => r.Goat)
            .Include(r => r.Medication)
            .FirstOrDefaultAsync(r => r.Id == id);
        return record is null ? NotFound() : record;
    }

    [HttpPost("records")]
    public async Task<ActionResult<MedicalRecord>> CreateRecord(MedicalRecord record)
    {
        record.CreatedAt = DateTime.UtcNow;

        // Auto-calculate next due date from recurrence
        if (record.Recurrence != RecurrenceInterval.None)
            record.NextDueDate = CalculateNextDue(record.Date, record.Recurrence);

        _db.MedicalRecords.Add(record);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetRecord), new { id = record.Id }, record);
    }

    [HttpPut("records/{id}")]
    public async Task<IActionResult> UpdateRecord(int id, MedicalRecord record)
    {
        if (id != record.Id) return BadRequest();
        var existing = await _db.MedicalRecords.FindAsync(id);
        if (existing is null) return NotFound();

        existing.RecordType = record.RecordType;
        existing.Title = record.Title;
        existing.Description = record.Description;
        existing.Date = record.Date;
        existing.MedicationId = record.MedicationId;
        existing.Dosage = record.Dosage;
        existing.AdministeredBy = record.AdministeredBy;
        existing.Recurrence = record.Recurrence;
        existing.NextDueDate = record.Recurrence != RecurrenceInterval.None
            ? CalculateNextDue(record.Date, record.Recurrence)
            : null;
        existing.Notes = record.Notes;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("records/{id}")]
    public async Task<IActionResult> DeleteRecord(int id)
    {
        var record = await _db.MedicalRecords.FindAsync(id);
        if (record is null) return NotFound();
        _db.MedicalRecords.Remove(record);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("overdue")]
    public async Task<ActionResult<List<MedicalRecord>>> GetOverdue()
    {
        return await _db.MedicalRecords
            .Include(r => r.Goat)
            .Where(r => r.NextDueDate != null && r.NextDueDate <= DateTime.UtcNow)
            .OrderBy(r => r.NextDueDate)
            .ToListAsync();
    }

    [HttpGet("upcoming")]
    public async Task<ActionResult<List<MedicalRecord>>> GetUpcoming([FromQuery] int days = 14)
    {
        var cutoff = DateTime.UtcNow.AddDays(days);
        return await _db.MedicalRecords
            .Include(r => r.Goat)
            .Where(r => r.NextDueDate != null && r.NextDueDate > DateTime.UtcNow && r.NextDueDate <= cutoff)
            .OrderBy(r => r.NextDueDate)
            .ToListAsync();
    }

    // --- Medications ---

    [HttpGet("medications")]
    public async Task<ActionResult<List<Medication>>> GetMedications()
    {
        return await _db.Medications.OrderBy(m => m.Name).ToListAsync();
    }

    [HttpGet("medications/{id}")]
    public async Task<ActionResult<Medication>> GetMedication(int id)
    {
        var med = await _db.Medications.FindAsync(id);
        return med is null ? NotFound() : med;
    }

    [HttpPost("medications")]
    public async Task<ActionResult<Medication>> CreateMedication(Medication med)
    {
        _db.Medications.Add(med);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetMedication), new { id = med.Id }, med);
    }

    [HttpPut("medications/{id}")]
    public async Task<IActionResult> UpdateMedication(int id, Medication med)
    {
        if (id != med.Id) return BadRequest();
        var existing = await _db.Medications.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = med.Name;
        existing.Description = med.Description;
        existing.DosageRate = med.DosageRate;
        existing.DosagePerPound = med.DosagePerPound;
        existing.Route = med.Route;
        existing.MeatWithdrawalDays = med.MeatWithdrawalDays;
        existing.MilkWithdrawalDays = med.MilkWithdrawalDays;
        existing.Notes = med.Notes;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("medications/{id}")]
    public async Task<IActionResult> DeleteMedication(int id)
    {
        var med = await _db.Medications.FindAsync(id);
        if (med is null) return NotFound();
        _db.Medications.Remove(med);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Medicine Cabinet ---

    [HttpGet("cabinet")]
    public async Task<ActionResult<List<MedicineCabinetItem>>> GetCabinet()
    {
        return await _db.MedicineCabinetItems
            .Include(c => c.Medication)
            .OrderBy(c => c.Medication.Name)
            .ToListAsync();
    }

    [HttpPost("cabinet")]
    public async Task<ActionResult<MedicineCabinetItem>> CreateCabinetItem(MedicineCabinetItem item)
    {
        item.LastUpdated = DateTime.UtcNow;
        _db.MedicineCabinetItems.Add(item);
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpPut("cabinet/{id}")]
    public async Task<IActionResult> UpdateCabinetItem(int id, MedicineCabinetItem item)
    {
        if (id != item.Id) return BadRequest();
        var existing = await _db.MedicineCabinetItems.FindAsync(id);
        if (existing is null) return NotFound();

        existing.MedicationId = item.MedicationId;
        existing.Quantity = item.Quantity;
        existing.Unit = item.Unit;
        existing.ExpirationDate = item.ExpirationDate;
        existing.LotNumber = item.LotNumber;
        existing.LastUpdated = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("cabinet/{id}")]
    public async Task<IActionResult> DeleteCabinetItem(int id)
    {
        var item = await _db.MedicineCabinetItems.FindAsync(id);
        if (item is null) return NotFound();
        _db.MedicineCabinetItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Weight Records ---

    [HttpGet("weights/{goatId}")]
    public async Task<ActionResult<List<WeightRecord>>> GetWeights(int goatId)
    {
        return await _db.WeightRecords
            .Where(w => w.GoatId == goatId)
            .OrderByDescending(w => w.Date)
            .ToListAsync();
    }

    [HttpPost("weights")]
    public async Task<ActionResult<WeightRecord>> CreateWeight(WeightRecord record)
    {
        _db.WeightRecords.Add(record);
        await _db.SaveChangesAsync();
        return Ok(record);
    }

    [HttpDelete("weights/{id}")]
    public async Task<IActionResult> DeleteWeight(int id)
    {
        var record = await _db.WeightRecords.FindAsync(id);
        if (record is null) return NotFound();
        _db.WeightRecords.Remove(record);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- FAMACHA Scores ---

    [HttpGet("famacha/{goatId}")]
    public async Task<ActionResult<List<FamachaScore>>> GetFamacha(int goatId)
    {
        return await _db.FamachaScores
            .Where(f => f.GoatId == goatId)
            .OrderByDescending(f => f.Date)
            .ToListAsync();
    }

    [HttpPost("famacha")]
    public async Task<ActionResult<FamachaScore>> CreateFamacha(FamachaScore score)
    {
        _db.FamachaScores.Add(score);
        await _db.SaveChangesAsync();
        return Ok(score);
    }

    [HttpDelete("famacha/{id}")]
    public async Task<IActionResult> DeleteFamacha(int id)
    {
        var score = await _db.FamachaScores.FindAsync(id);
        if (score is null) return NotFound();
        _db.FamachaScores.Remove(score);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Body Condition Scores ---

    [HttpGet("bcs/{goatId}")]
    public async Task<ActionResult<List<BodyConditionScore>>> GetBcs(int goatId)
    {
        return await _db.BodyConditionScores
            .Where(b => b.GoatId == goatId)
            .OrderByDescending(b => b.Date)
            .ToListAsync();
    }

    [HttpPost("bcs")]
    public async Task<ActionResult<BodyConditionScore>> CreateBcs(BodyConditionScore score)
    {
        _db.BodyConditionScores.Add(score);
        await _db.SaveChangesAsync();
        return Ok(score);
    }

    [HttpDelete("bcs/{id}")]
    public async Task<IActionResult> DeleteBcs(int id)
    {
        var score = await _db.BodyConditionScores.FindAsync(id);
        if (score is null) return NotFound();
        _db.BodyConditionScores.Remove(score);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Dosage Calculator ---

    [HttpGet("dosage-calc")]
    public async Task<ActionResult<object>> CalculateDosage([FromQuery] int medicationId, [FromQuery] double weightLbs)
    {
        var med = await _db.Medications.FindAsync(medicationId);
        if (med is null) return NotFound("Medication not found");
        if (med.DosagePerPound is null) return BadRequest("Medication has no dosage rate configured");

        var dose = weightLbs * med.DosagePerPound.Value;
        return Ok(new
        {
            medication = med.Name,
            weightLbs,
            dosageRate = med.DosageRate,
            calculatedDoseMl = Math.Round(dose, 2),
            route = med.Route,
            meatWithdrawalDays = med.MeatWithdrawalDays,
            milkWithdrawalDays = med.MilkWithdrawalDays
        });
    }

    // --- Herd Health Summary ---

    [HttpGet("summary")]
    public async Task<ActionResult<object>> GetHealthSummary()
    {
        var avgFamacha = await _db.FamachaScores
            .GroupBy(f => f.GoatId)
            .Select(g => g.OrderByDescending(f => f.Date).First())
            .AverageAsync(f => (double?)f.Score);

        var avgBcs = await _db.BodyConditionScores
            .GroupBy(b => b.GoatId)
            .Select(g => g.OrderByDescending(b => b.Date).First())
            .AverageAsync(b => (double?)b.Score);

        var overdue = await _db.MedicalRecords
            .CountAsync(r => r.NextDueDate != null && r.NextDueDate <= DateTime.UtcNow);

        var expiringSoon = await _db.MedicineCabinetItems
            .CountAsync(c => c.ExpirationDate != null && c.ExpirationDate <= DateTime.UtcNow.AddDays(30));

        return Ok(new
        {
            averageFamachaScore = avgFamacha.HasValue ? Math.Round(avgFamacha.Value, 1) : (double?)null,
            averageBodyConditionScore = avgBcs.HasValue ? Math.Round(avgBcs.Value, 1) : (double?)null,
            overdueRecords = overdue,
            medicationsExpiringSoon = expiringSoon
        });
    }

    /// <summary>
    /// Per-goat health "needs attention" view — pulls overdue meds, FAMACHA at risk,
    /// BCS slipping, weight loss flags, sick status into one structured response.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<object>> GetHealthDashboard()
    {
        var now = DateTime.UtcNow;
        var monthAgo = now.AddDays(-30);

        // Overdue + upcoming medical records
        var overdue = await _db.MedicalRecords
            .Include(r => r.Goat)
            .Where(r => r.NextDueDate != null && r.NextDueDate <= now)
            .OrderBy(r => r.NextDueDate)
            .Select(r => new {
                r.Id, r.Title, r.GoatId, goatName = r.Goat.Name, dueDate = r.NextDueDate,
                daysOverdue = (int)(now - r.NextDueDate!.Value).TotalDays
            })
            .ToListAsync();

        var upcoming = await _db.MedicalRecords
            .Include(r => r.Goat)
            .Where(r => r.NextDueDate != null && r.NextDueDate > now && r.NextDueDate <= now.AddDays(14))
            .OrderBy(r => r.NextDueDate)
            .Select(r => new {
                r.Id, r.Title, r.GoatId, goatName = r.Goat.Name, dueDate = r.NextDueDate,
                daysUntil = (int)(r.NextDueDate!.Value - now).TotalDays
            })
            .ToListAsync();

        // FAMACHA at risk: latest score per goat, score >= 3
        var allFamacha = await _db.FamachaScores.Include(f => f.Goat).ToListAsync();
        var latestFamacha = allFamacha
            .GroupBy(f => f.GoatId)
            .Select(g => g.OrderByDescending(f => f.Date).First())
            .Where(f => f.Score >= 3)
            .Select(f => new { f.GoatId, goatName = f.Goat.Name, score = f.Score, date = f.Date })
            .ToList();

        // BCS slipping: latest BCS <= 2 OR >= 4
        var allBcs = await _db.BodyConditionScores.Include(b => b.Goat).ToListAsync();
        var latestBcs = allBcs
            .GroupBy(b => b.GoatId)
            .Select(g => g.OrderByDescending(b => b.Date).First())
            .Where(b => b.Score <= 2 || b.Score >= 4)
            .Select(b => new {
                b.GoatId, goatName = b.Goat.Name, score = b.Score, date = b.Date,
                concern = b.Score <= 2 ? "underweight" : "overweight"
            })
            .ToList();

        // Weight loss flag: latest weight in last 30 days lower than the previous one
        var weightAlerts = new List<object>();
        var weights = await _db.WeightRecords
            .Include(w => w.Goat)
            .Where(w => w.Date >= monthAgo.AddDays(-60))
            .OrderBy(w => w.GoatId).ThenByDescending(w => w.Date)
            .ToListAsync();
        foreach (var grp in weights.GroupBy(w => w.GoatId))
        {
            var ordered = grp.OrderByDescending(w => w.Date).ToList();
            if (ordered.Count >= 2)
            {
                var latest = ordered[0];
                var prior = ordered[1];
                if (latest.Date >= monthAgo && latest.Weight < prior.Weight)
                {
                    var lossPct = (prior.Weight - latest.Weight) / prior.Weight * 100;
                    if (lossPct >= 5) // ignore tiny scale wobble
                    {
                        weightAlerts.Add(new {
                            goatId = latest.GoatId,
                            goatName = latest.Goat.Name,
                            latestLbs = latest.Weight,
                            priorLbs = prior.Weight,
                            lossPct = Math.Round(lossPct, 1),
                            since = prior.Date
                        });
                    }
                }
            }
        }

        var sick = await _db.Goats
            .Where(g => !g.IsExternal && (g.Status == GoatStatus.Sick || g.Status == GoatStatus.AtVet))
            .Select(g => new { g.Id, g.Name, status = g.Status.ToString() })
            .ToListAsync();

        var expiringMeds = await _db.MedicineCabinetItems
            .Include(m => m.Medication)
            .Where(m => m.ExpirationDate != null && m.ExpirationDate <= now.AddDays(30))
            .OrderBy(m => m.ExpirationDate)
            .Select(m => new {
                m.Id, name = m.Medication.Name, expirationDate = m.ExpirationDate, m.Quantity, m.Unit,
                expired = m.ExpirationDate < now
            })
            .ToListAsync();

        return Ok(new
        {
            overdueMedical = overdue,
            upcomingMedical = upcoming,
            famachaAtRisk = latestFamacha,
            bcsConcerns = latestBcs,
            weightLoss = weightAlerts,
            sickGoats = sick,
            expiringMeds,
            counts = new
            {
                overdue = overdue.Count,
                upcoming = upcoming.Count,
                famacha = latestFamacha.Count,
                bcs = latestBcs.Count,
                weightLoss = weightAlerts.Count,
                sick = sick.Count,
                expiringMeds = expiringMeds.Count
            }
        });
    }

    private static DateTime CalculateNextDue(DateTime from, RecurrenceInterval interval) => interval switch
    {
        RecurrenceInterval.Weekly => from.AddDays(7),
        RecurrenceInterval.BiWeekly => from.AddDays(14),
        RecurrenceInterval.Monthly => from.AddMonths(1),
        RecurrenceInterval.Quarterly => from.AddMonths(3),
        RecurrenceInterval.BiAnnually => from.AddMonths(6),
        RecurrenceInterval.Annually => from.AddYears(1),
        _ => from
    };
}
