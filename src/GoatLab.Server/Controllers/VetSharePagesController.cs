using GoatLab.Server.Data;
using GoatLab.Server.Services;
using GoatLab.Server.Services.Vet;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Anonymous SSR view for /vet/{token}. Vet sees one goat's medical/weight/
// FAMACHA/body-condition history and can submit a note via plain HTML form.
//
// Privacy: nothing about other goats, the herd, billing, or any tenant
// metadata is exposed. Just the focal goat's basic identity + health data.
[AllowAnonymous]
public class VetSharePagesController : Controller
{
    private readonly VetShareLinkService _service;
    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;

    public VetSharePagesController(VetShareLinkService service, GoatLabDbContext db, ITenantContext tenantContext)
    {
        _service = service;
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet("/vet/{token}")]
    public async Task<IActionResult> Show(string token, CancellationToken ct)
    {
        var link = await _service.ResolveTokenAsync(token, ct);
        if (link is null) return View("VetShareInvalid");
        await _service.RecordViewAsync(link.Id, ct);

        // Cross-tenant — anon caller has no tenant claim, so bypass filter to
        // load child records for the focal goat. Scope is enforced by the
        // matched (and validated) token, not by the query filter.
        _tenantContext.BypassFilter = true;

        var goat = await _db.Goats.IgnoreQueryFilters()
            .Where(g => g.Id == link.GoatId)
            .Select(g => new VetShareGoatVm
            {
                Id = g.Id, Name = g.Name, EarTag = g.EarTag, Breed = g.Breed,
                Gender = g.Gender.ToString(),
                DateOfBirth = g.DateOfBirth,
                RegistrationNumber = g.RegistrationNumber,
                FarmName = g.Tenant!.Name,
            })
            .FirstOrDefaultAsync(ct);
        if (goat is null) return View("VetShareInvalid");

        goat.Medical = await _db.MedicalRecords.IgnoreQueryFilters()
            .Where(m => m.GoatId == link.GoatId)
            .OrderByDescending(m => m.Date)
            .Take(50)
            .Select(m => new VetMedicalVm(m.Date, m.RecordType.ToString(), m.Title, m.Description, m.Dosage, m.AdministeredBy))
            .ToListAsync(ct);

        goat.Weights = await _db.WeightRecords.IgnoreQueryFilters()
            .Where(w => w.GoatId == link.GoatId)
            .OrderByDescending(w => w.Date)
            .Take(20)
            .Select(w => new VetWeightVm(w.Date, w.Weight))
            .ToListAsync(ct);

        goat.Famacha = await _db.FamachaScores.IgnoreQueryFilters()
            .Where(f => f.GoatId == link.GoatId)
            .OrderByDescending(f => f.Date)
            .Take(10)
            .Select(f => new VetFamachaVm(f.Date, f.Score))
            .ToListAsync(ct);

        goat.BodyCondition = await _db.BodyConditionScores.IgnoreQueryFilters()
            .Where(b => b.GoatId == link.GoatId)
            .OrderByDescending(b => b.Date)
            .Take(10)
            .Select(b => new VetBodyConditionVm(b.Date, b.Score))
            .ToListAsync(ct);

        ViewData["Token"] = token;
        ViewData["VetName"] = link.VetName;
        ViewData["ExpiresAt"] = link.ExpiresAt;
        ViewData["NoteSaved"] = TempData["NoteSaved"];
        return View(goat);
    }

    public record SubmitNoteForm(string VetName, string Note);

    [HttpPost("/vet/{token}/note")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitNote(string token, SubmitNoteForm form, CancellationToken ct)
    {
        var link = await _service.ResolveTokenAsync(token, ct);
        if (link is null) return View("VetShareInvalid");

        if (string.IsNullOrWhiteSpace(form.Note))
        {
            TempData["NoteSaved"] = "Note is empty — nothing was saved.";
            return RedirectToAction(nameof(Show), new { token });
        }

        var vetName = string.IsNullOrWhiteSpace(form.VetName) ? (link.VetName ?? "Visiting vet") : form.VetName.Trim();
        var noteText = form.Note.Trim();
        if (noteText.Length > 4000) noteText = noteText[..4000];

        _tenantContext.BypassFilter = true;

        // Persist as a Checkup-type MedicalRecord so the note shows up in the
        // standard health history and on the goat timeline. The owner sees it
        // immediately on their next visit to the goat detail page.
        var record = new MedicalRecord
        {
            TenantId = link.TenantId,
            GoatId = link.GoatId,
            RecordType = MedicalRecordType.Checkup,
            Title = $"Vet note from {vetName}",
            Description = noteText,
            Date = DateTime.UtcNow,
            AdministeredBy = vetName,
        };
        _db.MedicalRecords.Add(record);
        await _db.SaveChangesAsync(ct);

        TempData["NoteSaved"] = "Note saved — the farmer will see it on the goat's timeline.";
        return RedirectToAction(nameof(Show), new { token });
    }

    // ----- View models -----

    public class VetShareGoatVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? EarTag { get; set; }
        public string? Breed { get; set; }
        public string Gender { get; set; } = "";
        public DateTime? DateOfBirth { get; set; }
        public string? RegistrationNumber { get; set; }
        public string FarmName { get; set; } = "";
        public List<VetMedicalVm> Medical { get; set; } = new();
        public List<VetWeightVm> Weights { get; set; } = new();
        public List<VetFamachaVm> Famacha { get; set; } = new();
        public List<VetBodyConditionVm> BodyCondition { get; set; } = new();
    }

    public record VetMedicalVm(DateTime Date, string RecordType, string Title, string? Description, string? Dosage, string? AdministeredBy);
    public record VetWeightVm(DateTime Date, double Weight);
    public record VetFamachaVm(DateTime Date, int Score);
    public record VetBodyConditionVm(DateTime Date, double Score);
}
