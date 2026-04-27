using GoatLab.Server.Data;
using GoatLab.Server.Services.Plans;
using GoatLab.Server.Services.Webhooks;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequiresFeature(AppFeature.Goats)]
public class GoatsController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IFeatureGate _featureGate;
    private readonly WebhookDispatcher _webhooks;

    public GoatsController(GoatLabDbContext db, IWebHostEnvironment env, IFeatureGate featureGate, WebhookDispatcher webhooks)
    {
        _db = db;
        _env = env;
        _featureGate = featureGate;
        _webhooks = webhooks;
    }

    private object GoatSummary(Goat g) => new
    {
        g.Id, g.Name, g.EarTag, g.Breed, g.Gender, g.Status, g.DateOfBirth,
        g.SireId, g.DamId, g.RegistrationNumber, g.IsListedForSale
    };

    [HttpGet]
    public async Task<ActionResult<List<Goat>>> GetAll(
        [FromQuery] GoatStatus? status,
        [FromQuery] string? search,
        [FromQuery] bool includeExternal = false)
    {
        var query = _db.Goats
            .Include(g => g.Pen).ThenInclude(p => p!.Barn)
            .AsQueryable();

        if (!includeExternal)
            query = query.Where(g => !g.IsExternal);

        if (status.HasValue)
            query = query.Where(g => g.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(g =>
                g.Name.Contains(search)
                || (g.EarTag != null && g.EarTag.Contains(search))
                || (g.RegistrationNumber != null && g.RegistrationNumber.Contains(search))
                || (g.TattooLeft != null && g.TattooLeft.Contains(search))
                || (g.TattooRight != null && g.TattooRight.Contains(search))
                || (g.ScrapieTag != null && g.ScrapieTag.Contains(search))
                || (g.Microchip != null && g.Microchip.Contains(search))
                || (g.BreederName != null && g.BreederName.Contains(search)));

        return await query.OrderBy(g => g.Name).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Goat>> Get(int id)
    {
        var goat = await _db.Goats
            .Include(g => g.Sire)
            .Include(g => g.Dam)
            .Include(g => g.Pen).ThenInclude(p => p!.Barn)
            .Include(g => g.Photos)
            .Include(g => g.Documents)
            .FirstOrDefaultAsync(g => g.Id == id);

        return goat is null ? NotFound() : goat;
    }

    [HttpGet("{id}/pedigree")]
    public async Task<ActionResult<object>> GetPedigree(int id)
    {
        // Eager-load 3 generations of ancestors (parents, grandparents, great-grandparents)
        var goat = await _db.Goats
            .Include(g => g.Sire).ThenInclude(s => s!.Sire).ThenInclude(ss => ss!.Sire)
            .Include(g => g.Sire).ThenInclude(s => s!.Sire).ThenInclude(ss => ss!.Dam)
            .Include(g => g.Sire).ThenInclude(s => s!.Dam).ThenInclude(sd => sd!.Sire)
            .Include(g => g.Sire).ThenInclude(s => s!.Dam).ThenInclude(sd => sd!.Dam)
            .Include(g => g.Dam).ThenInclude(d => d!.Sire).ThenInclude(ds => ds!.Sire)
            .Include(g => g.Dam).ThenInclude(d => d!.Sire).ThenInclude(ds => ds!.Dam)
            .Include(g => g.Dam).ThenInclude(d => d!.Dam).ThenInclude(dd => dd!.Sire)
            .Include(g => g.Dam).ThenInclude(d => d!.Dam).ThenInclude(dd => dd!.Dam)
            .Include(g => g.OffspringAsSire)
            .Include(g => g.OffspringAsDam)
            .FirstOrDefaultAsync(g => g.Id == id);

        return goat is null ? NotFound() : goat;
    }

    [HttpPost]
    public async Task<ActionResult<Goat>> Create(Goat goat)
    {
        if (!await _featureGate.CanAddGoatAsync())
        {
            return new ObjectResult(new
            {
                error = "You have reached your plan's goat limit.",
                upgradeRequired = true,
                limit = "MaxGoats",
            })
            { StatusCode = StatusCodes.Status402PaymentRequired };
        }

        goat.CreatedAt = DateTime.UtcNow;
        goat.UpdatedAt = DateTime.UtcNow;
        goat.StatusChangedAt = DateTime.UtcNow;
        _db.Goats.Add(goat);
        await _db.SaveChangesAsync();
        await _webhooks.DispatchAsync(WebhookEventTypes.GoatCreated, GoatSummary(goat));
        return CreatedAtAction(nameof(Get), new { id = goat.Id }, goat);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Goat goat)
    {
        if (id != goat.Id) return BadRequest();

        var existing = await _db.Goats.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = goat.Name;
        existing.EarTag = goat.EarTag;
        existing.Breed = goat.Breed;
        existing.Gender = goat.Gender;
        existing.DateOfBirth = goat.DateOfBirth;
        // Stamp StatusChangedAt only when the status actually changes — that
        // way the mortality report's window filter is precise instead of being
        // dragged forward by unrelated field edits.
        if (existing.Status != goat.Status)
        {
            existing.StatusChangedAt = DateTime.UtcNow;
        }
        existing.Status = goat.Status;
        existing.Bio = goat.Bio;
        existing.RegistrationNumber = goat.RegistrationNumber;
        existing.Registry = goat.Registry;
        existing.TattooLeft = goat.TattooLeft;
        existing.TattooRight = goat.TattooRight;
        existing.ScrapieTag = goat.ScrapieTag;
        existing.Microchip = goat.Microchip;
        existing.BreederName = goat.BreederName;
        existing.IsExternal = goat.IsExternal;
        existing.SireId = goat.SireId;
        existing.DamId = goat.DamId;
        existing.PenId = goat.PenId;
        existing.IsListedForSale = goat.IsListedForSale;
        existing.AskingPriceCents = goat.AskingPriceCents;
        existing.SaleNotes = goat.SaleNotes;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _webhooks.DispatchAsync(WebhookEventTypes.GoatUpdated, GoatSummary(existing));
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var goat = await _db.Goats.FindAsync(id);
        if (goat is null) return NotFound();

        var summary = GoatSummary(goat);
        _db.Goats.Remove(goat);
        await _db.SaveChangesAsync();
        await _webhooks.DispatchAsync(WebhookEventTypes.GoatDeleted, summary);
        return NoContent();
    }

    // --- Photos ---

    [HttpPost("{id}/photos")]
    public async Task<ActionResult<GoatPhoto>> UploadPhoto(int id, IFormFile file, [FromForm] string? caption, [FromForm] bool isPrimary = false)
    {
        var goat = await _db.Goats.FindAsync(id);
        if (goat is null) return NotFound();

        var uploadsDir = Path.Combine(_env.ContentRootPath, "media", "goats", id.ToString());
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        if (isPrimary)
        {
            var existing = await _db.GoatPhotos.Where(p => p.GoatId == id && p.IsPrimary).ToListAsync();
            existing.ForEach(p => p.IsPrimary = false);
        }

        var photo = new GoatPhoto
        {
            GoatId = id,
            FilePath = $"media/goats/{id}/{fileName}",
            Caption = caption,
            IsPrimary = isPrimary
        };

        _db.GoatPhotos.Add(photo);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id }, photo);
    }

    [HttpDelete("{id}/photos/{photoId}")]
    public async Task<IActionResult> DeletePhoto(int id, int photoId)
    {
        var photo = await _db.GoatPhotos.FirstOrDefaultAsync(p => p.Id == photoId && p.GoatId == id);
        if (photo is null) return NotFound();

        var fullPath = Path.Combine(_env.ContentRootPath, photo.FilePath);
        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);

        _db.GoatPhotos.Remove(photo);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Documents ---

    [HttpPost("{id}/documents")]
    public async Task<ActionResult<GoatDocument>> UploadDocument(int id, IFormFile file, [FromForm] string title, [FromForm] string? documentType)
    {
        var goat = await _db.Goats.FindAsync(id);
        if (goat is null) return NotFound();

        var uploadsDir = Path.Combine(_env.ContentRootPath, "media", "documents", id.ToString());
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var doc = new GoatDocument
        {
            GoatId = id,
            Title = title,
            FilePath = $"media/documents/{id}/{fileName}",
            DocumentType = documentType
        };

        _db.GoatDocuments.Add(doc);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id }, doc);
    }

    [HttpDelete("{id}/documents/{docId}")]
    public async Task<IActionResult> DeleteDocument(int id, int docId)
    {
        var doc = await _db.GoatDocuments.FirstOrDefaultAsync(d => d.Id == docId && d.GoatId == id);
        if (doc is null) return NotFound();

        var fullPath = Path.Combine(_env.ContentRootPath, doc.FilePath);
        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);

        _db.GoatDocuments.Remove(doc);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Stats ---

    [HttpGet("stats")]
    public async Task<ActionResult<object>> GetStats()
    {
        var total = await _db.Goats.CountAsync(g => !g.IsExternal && g.Status != GoatStatus.Deceased && g.Status != GoatStatus.Sold);
        var sick = await _db.Goats.CountAsync(g => !g.IsExternal && g.Status == GoatStatus.Sick);
        var atVet = await _db.Goats.CountAsync(g => !g.IsExternal && g.Status == GoatStatus.AtVet);
        var pregnant = await _db.BreedingRecords.CountAsync(b => b.Outcome == BreedingOutcome.Confirmed && b.EstimatedDueDate > DateTime.UtcNow);
        var bucks = await _db.Goats.CountAsync(g => !g.IsExternal && g.Gender == Gender.Male && g.Status == GoatStatus.Healthy);
        var does = await _db.Goats.CountAsync(g => !g.IsExternal && g.Gender == Gender.Female && g.Status == GoatStatus.Healthy);

        return new { total, sick, atVet, pregnant, bucks, does };
    }
}
