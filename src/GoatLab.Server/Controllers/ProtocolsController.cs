using GoatLab.Server.Data;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequiresFeature(AppFeature.Health)]
public class ProtocolsController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public ProtocolsController(GoatLabDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<VaccinationProtocol>>> GetAll()
    {
        // Lazy-seed built-ins on first access — easier than baking into HasData seed migrations.
        if (!await _db.VaccinationProtocols.AnyAsync())
        {
            await SeedBuiltInsAsync();
        }
        return await _db.VaccinationProtocols
            .Include(p => p.Doses).ThenInclude(d => d.Medication)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VaccinationProtocol>> Get(int id)
    {
        var p = await _db.VaccinationProtocols
            .Include(x => x.Doses).ThenInclude(d => d.Medication)
            .FirstOrDefaultAsync(x => x.Id == id);
        return p is null ? NotFound() : p;
    }

    [HttpPost]
    public async Task<ActionResult<VaccinationProtocol>> Create(VaccinationProtocol protocol)
    {
        protocol.IsBuiltIn = false;
        protocol.CreatedAt = DateTime.UtcNow;
        _db.VaccinationProtocols.Add(protocol);
        await _db.SaveChangesAsync();
        return Ok(protocol);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, VaccinationProtocol protocol)
    {
        if (id != protocol.Id) return BadRequest();
        var existing = await _db.VaccinationProtocols
            .Include(p => p.Doses)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (existing is null) return NotFound();

        existing.Name = protocol.Name;
        existing.Description = protocol.Description;
        existing.AppliesTo = protocol.AppliesTo;

        // Replace doses wholesale (simpler than diffing)
        _db.ProtocolDoses.RemoveRange(existing.Doses);
        existing.Doses = protocol.Doses?.Select(d => new ProtocolDose
        {
            Title = d.Title,
            RecordType = d.RecordType,
            MedicationId = d.MedicationId,
            DayOffset = d.DayOffset,
            Recurrence = d.Recurrence,
            SortOrder = d.SortOrder,
            Notes = d.Notes
        }).ToList() ?? new();

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _db.VaccinationProtocols.FindAsync(id);
        if (p is null) return NotFound();
        _db.VaccinationProtocols.Remove(p);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Apply a protocol to one or more goats. Each dose becomes a MedicalRecord
    /// dated (startDate + dose.DayOffset). Recurrence is copied so existing
    /// NextDueDate logic in HealthController continues to work for boosters.
    /// </summary>
    [HttpPost("{id}/apply")]
    public async Task<ActionResult<object>> Apply(int id, [FromBody] ApplyProtocolRequest req)
    {
        var protocol = await _db.VaccinationProtocols
            .Include(p => p.Doses).ThenInclude(d => d.Medication)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (protocol is null) return NotFound("Protocol not found");
        if (req.GoatIds is null || !req.GoatIds.Any()) return BadRequest("Pick at least one goat");

        var startDate = req.StartDate ?? DateTime.UtcNow;
        int created = 0;

        foreach (var goatId in req.GoatIds)
        {
            var goat = await _db.Goats.FindAsync(goatId);
            if (goat is null) continue;

            foreach (var dose in protocol.Doses.OrderBy(d => d.DayOffset))
            {
                var doseDate = startDate.AddDays(dose.DayOffset);
                var nextDue = dose.Recurrence != RecurrenceInterval.None
                    ? CalcNextDue(doseDate, dose.Recurrence)
                    : (DateTime?)null;

                _db.MedicalRecords.Add(new MedicalRecord
                {
                    GoatId = goat.Id,
                    RecordType = dose.RecordType,
                    Title = dose.Title,
                    Description = $"Auto-scheduled from protocol: {protocol.Name}",
                    Date = doseDate,
                    MedicationId = dose.MedicationId,
                    Recurrence = dose.Recurrence,
                    NextDueDate = nextDue,
                    Notes = dose.Notes,
                    CreatedAt = DateTime.UtcNow
                });
                created++;
            }
        }
        await _db.SaveChangesAsync();
        return Ok(new { protocolId = id, recordsCreated = created, goatsCovered = req.GoatIds.Count });
    }

    private static DateTime CalcNextDue(DateTime baseDate, RecurrenceInterval r) => r switch
    {
        RecurrenceInterval.Weekly      => baseDate.AddDays(7),
        RecurrenceInterval.BiWeekly    => baseDate.AddDays(14),
        RecurrenceInterval.Monthly     => baseDate.AddMonths(1),
        RecurrenceInterval.Quarterly   => baseDate.AddMonths(3),
        RecurrenceInterval.BiAnnually  => baseDate.AddMonths(6),
        RecurrenceInterval.Annually    => baseDate.AddYears(1),
        _ => baseDate
    };

    private async Task SeedBuiltInsAsync()
    {
        // Resolve "CDT" medication if present, else leave null
        var cdt = await _db.Medications.FirstOrDefaultAsync(m => m.Name.Contains("CDT") || m.Name.Contains("CD&T"));
        var dewormer = await _db.Medications.FirstOrDefaultAsync(m =>
            m.Name.Contains("Ivermec") || m.Name.Contains("Cydec") || m.Name.Contains("dewormer"));

        var protocols = new[]
        {
            new VaccinationProtocol
            {
                Name = "CDT — Adult annual",
                Description = "Standard CDT (Clostridium perfringens C&D + Tetanus) adult booster, every 12 months.",
                AppliesTo = "Adult",
                IsBuiltIn = true,
                Doses = new List<ProtocolDose>
                {
                    new() { Title = "CDT booster", RecordType = MedicalRecordType.Vaccination, MedicationId = cdt?.Id,
                            DayOffset = 0, Recurrence = RecurrenceInterval.Annually, SortOrder = 0,
                            Notes = "Subcutaneous, 2mL behind the elbow." }
                }
            },
            new VaccinationProtocol
            {
                Name = "CDT — Kid series",
                Description = "Two-dose primary series for kids: first dose at 4–8 weeks, booster 4 weeks later, then annual.",
                AppliesTo = "Kid",
                IsBuiltIn = true,
                Doses = new List<ProtocolDose>
                {
                    new() { Title = "CDT — first dose", RecordType = MedicalRecordType.Vaccination, MedicationId = cdt?.Id,
                            DayOffset = 0, SortOrder = 0, Notes = "Subcutaneous, 2mL." },
                    new() { Title = "CDT — booster",    RecordType = MedicalRecordType.Vaccination, MedicationId = cdt?.Id,
                            DayOffset = 28, Recurrence = RecurrenceInterval.Annually, SortOrder = 1,
                            Notes = "4 weeks after first dose; then yearly." }
                }
            },
            new VaccinationProtocol
            {
                Name = "Deworming — Strategic rotation",
                Description = "Targeted deworming based on FAMACHA, every 90 days as a baseline check.",
                AppliesTo = null,
                IsBuiltIn = true,
                Doses = new List<ProtocolDose>
                {
                    new() { Title = "FAMACHA check + deworm if needed", RecordType = MedicalRecordType.Deworming,
                            MedicationId = dewormer?.Id, DayOffset = 0,
                            Recurrence = RecurrenceInterval.Quarterly, SortOrder = 0,
                            Notes = "Only treat goats scoring 3, 4, or 5 to slow resistance." }
                }
            },
            new VaccinationProtocol
            {
                Name = "Pre-kidding doe",
                Description = "Booster CDT to does 4 weeks before kidding so colostrum carries antibodies to kids.",
                AppliesTo = "Doe",
                IsBuiltIn = true,
                Doses = new List<ProtocolDose>
                {
                    new() { Title = "Pre-kidding CDT", RecordType = MedicalRecordType.Vaccination, MedicationId = cdt?.Id,
                            DayOffset = 0, SortOrder = 0,
                            Notes = "Time so dose lands ~4 weeks before estimated due date." }
                }
            }
        };

        _db.VaccinationProtocols.AddRange(protocols);
        await _db.SaveChangesAsync();
    }
}

public class ApplyProtocolRequest
{
    public List<int> GoatIds { get; set; } = new();
    public DateTime? StartDate { get; set; }
}
