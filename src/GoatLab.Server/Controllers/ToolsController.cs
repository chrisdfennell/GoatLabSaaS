using System.Globalization;
using System.IO.Compression;
using CsvHelper;
using CsvHelper.Configuration;
using GoatLab.Server.Data;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ToolsController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public ToolsController(GoatLabDbContext db, IWebHostEnvironment env, IConfiguration config)
    {
        _db = db;
        _env = env;
        _config = config;
    }

    // --- Database Backup ---

    [HttpPost("backup/database")]
    public IActionResult BackupDatabase()
    {
        var dbPath = _config.GetValue<string>("DatabasePath") ?? "goatlab.db";
        if (!System.IO.File.Exists(dbPath))
            return NotFound("Database file not found");

        var backupDir = Path.Combine(_env.ContentRootPath, "backups");
        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var backupPath = Path.Combine(backupDir, $"goatlab-backup-{timestamp}.db");
        System.IO.File.Copy(dbPath, backupPath, overwrite: true);

        var bytes = System.IO.File.ReadAllBytes(backupPath);
        return File(bytes, "application/octet-stream", Path.GetFileName(backupPath));
    }

    [HttpPost("restore/database")]
    public async Task<IActionResult> RestoreDatabase(IFormFile file)
    {
        var dbPath = _config.GetValue<string>("DatabasePath") ?? "goatlab.db";

        // Save backup of current DB first
        if (System.IO.File.Exists(dbPath))
        {
            var backupDir = Path.Combine(_env.ContentRootPath, "backups");
            Directory.CreateDirectory(backupDir);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            System.IO.File.Copy(dbPath, Path.Combine(backupDir, $"goatlab-pre-restore-{timestamp}.db"), overwrite: true);
        }

        // Write uploaded file as new DB
        await using var stream = new FileStream(dbPath, FileMode.Create);
        await file.CopyToAsync(stream);

        return Ok(new { message = "Database restored. Restart the application to apply changes." });
    }

    // --- Media Backup ---

    [HttpPost("backup/media")]
    public IActionResult BackupMedia()
    {
        var mediaDir = Path.Combine(_env.ContentRootPath, "media");
        if (!Directory.Exists(mediaDir))
            return NotFound("Media directory not found");

        var backupDir = Path.Combine(_env.ContentRootPath, "backups");
        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var zipPath = Path.Combine(backupDir, $"goatlab-media-{timestamp}.zip");
        ZipFile.CreateFromDirectory(mediaDir, zipPath);

        var bytes = System.IO.File.ReadAllBytes(zipPath);
        return File(bytes, "application/zip", Path.GetFileName(zipPath));
    }

    // --- CSV Export ---

    [HttpGet("export/goats")]
    public async Task<IActionResult> ExportGoats()
    {
        var goats = await _db.Goats.Include(g => g.Pen).ThenInclude(p => p!.Barn).ToListAsync();
        var csv = ToCsv(goats.Select(g => new
        {
            g.Id, g.Name, g.EarTag, g.Breed, Gender = g.Gender.ToString(), DOB = g.DateOfBirth?.ToString("yyyy-MM-dd"),
            Status = g.Status.ToString(), Pen = g.Pen?.Name, Barn = g.Pen?.Barn?.Name
        }));
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "goats.csv");
    }

    // --- CSV Import ---

    /// <summary>
    /// Blank CSV with the column headers we accept on import. Mirrors the export
    /// format so round-tripping an export works. Header names are matched
    /// case-insensitively and with underscores/spaces stripped.
    /// </summary>
    [HttpGet("import/goats/template")]
    public IActionResult GetGoatImportTemplate()
    {
        var header = string.Join(",", GoatImportColumns);
        var example = "Pearl,A-001,Nubian,Female,2022-04-15,Healthy,Gentle first-freshener,";
        var csv = header + "\n" + example + "\n";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "goat-import-template.csv");
    }

    private static readonly string[] GoatImportColumns = new[]
    {
        "Name", "EarTag", "Breed", "Gender", "DateOfBirth",
        "Status", "Bio", "RegistrationNumber"
    };

    [HttpPost("import/goats")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<GoatImportResult>> ImportGoats(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        var errors = new List<ImportRowError>();
        var toAdd = new List<Goat>();
        var rowNum = 1; // header row = 1; first data row = 2

        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            PrepareHeaderForMatch = args => args.Header.Trim().Replace("_", "").Replace(" ", "").ToLowerInvariant(),
            MissingFieldFound = null, // missing optional columns are fine
            BadDataFound = null,
        };

        using var reader = new StreamReader(file.OpenReadStream());
        using var csv = new CsvReader(reader, cfg);

        try
        {
            await csv.ReadAsync();
            csv.ReadHeader();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Couldn't read CSV header: {ex.Message}" });
        }

        while (await csv.ReadAsync())
        {
            rowNum++;
            try
            {
                var name = csv.TryGetField<string>("Name", out var n) ? n?.Trim() : null;
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(new ImportRowError(rowNum, "Name is required."));
                    continue;
                }

                var goat = new Goat
                {
                    Name = name!,
                    EarTag = NullIfEmpty(csv.TryGetField<string>("EarTag", out var t) ? t : null),
                    Breed = NullIfEmpty(csv.TryGetField<string>("Breed", out var b) ? b : null),
                    Bio = NullIfEmpty(csv.TryGetField<string>("Bio", out var bio) ? bio : null),
                    RegistrationNumber = NullIfEmpty(csv.TryGetField<string>("RegistrationNumber", out var reg) ? reg : null),
                };

                if (csv.TryGetField<string>("Gender", out var gen) && !string.IsNullOrWhiteSpace(gen))
                {
                    if (Enum.TryParse<Gender>(gen.Trim(), ignoreCase: true, out var g))
                        goat.Gender = g;
                    else
                    {
                        errors.Add(new ImportRowError(rowNum, $"Unknown Gender '{gen}'. Use Male, Female, or Wether."));
                        continue;
                    }
                }

                if (csv.TryGetField<string>("Status", out var st) && !string.IsNullOrWhiteSpace(st))
                {
                    if (Enum.TryParse<GoatStatus>(st.Trim(), ignoreCase: true, out var s))
                        goat.Status = s;
                    else
                    {
                        errors.Add(new ImportRowError(rowNum, $"Unknown Status '{st}'."));
                        continue;
                    }
                }

                if (csv.TryGetField<string>("DateOfBirth", out var dob) && !string.IsNullOrWhiteSpace(dob))
                {
                    if (DateTime.TryParse(dob.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                        goat.DateOfBirth = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
                    else
                    {
                        errors.Add(new ImportRowError(rowNum, $"Couldn't parse DateOfBirth '{dob}'. Use yyyy-MM-dd."));
                        continue;
                    }
                }

                toAdd.Add(goat);
            }
            catch (Exception ex)
            {
                errors.Add(new ImportRowError(rowNum, ex.Message));
            }
        }

        if (toAdd.Count > 0)
        {
            _db.Goats.AddRange(toAdd);
            await _db.SaveChangesAsync();
        }

        var total = rowNum - 1; // minus header
        return new GoatImportResult(total, toAdd.Count, total - toAdd.Count, errors);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    [HttpGet("export/milk-logs")]
    public async Task<IActionResult> ExportMilkLogs([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var query = _db.MilkLogs.Include(m => m.Goat).AsQueryable();
        if (from.HasValue) query = query.Where(m => m.Date >= from.Value);
        if (to.HasValue) query = query.Where(m => m.Date <= to.Value);

        var logs = await query.OrderByDescending(m => m.Date).ToListAsync();
        var csv = ToCsv(logs.Select(l => new
        {
            l.Id, GoatName = l.Goat.Name, Date = l.Date.ToString("yyyy-MM-dd"), AmountLbs = l.Amount, l.Notes
        }));
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "milk-logs.csv");
    }

    [HttpGet("export/medical-records")]
    public async Task<IActionResult> ExportMedicalRecords()
    {
        var records = await _db.MedicalRecords.Include(r => r.Goat).Include(r => r.Medication).ToListAsync();
        var csv = ToCsv(records.Select(r => new
        {
            r.Id, GoatName = r.Goat.Name, Type = r.RecordType.ToString(), r.Title,
            Date = r.Date.ToString("yyyy-MM-dd"), Medication = r.Medication?.Name, r.Dosage, r.Notes
        }));
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "medical-records.csv");
    }

    [HttpGet("export/finances")]
    public async Task<IActionResult> ExportFinances([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var query = _db.Transactions.Include(t => t.Goat).AsQueryable();
        if (from.HasValue) query = query.Where(t => t.Date >= from.Value);
        if (to.HasValue) query = query.Where(t => t.Date <= to.Value);

        var txns = await query.OrderByDescending(t => t.Date).ToListAsync();
        var csv = ToCsv(txns.Select(t => new
        {
            t.Id, Type = t.Type.ToString(), Date = t.Date.ToString("yyyy-MM-dd"),
            t.Description, Amount = t.Amount, t.Category, GoatName = t.Goat?.Name, t.Notes
        }));
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "finances.csv");
    }

    // --- Dashboard Activity Feed ---

    [HttpGet("activity")]
    public async Task<ActionResult<object>> GetRecentActivity([FromQuery] int count = 20)
    {
        var recentGoats = await _db.Goats.OrderByDescending(g => g.CreatedAt).Take(5)
            .Select(g => new { type = "goat_added", date = g.CreatedAt, description = $"Added goat: {g.Name}" })
            .ToListAsync();

        var recentMedical = await _db.MedicalRecords.Include(r => r.Goat).OrderByDescending(r => r.CreatedAt).Take(5)
            .Select(r => new { type = "medical", date = r.CreatedAt, description = $"{r.RecordType}: {r.Goat.Name} — {r.Title}" })
            .ToListAsync();

        var recentMilk = await _db.MilkLogs.Include(m => m.Goat).OrderByDescending(m => m.CreatedAt).Take(5)
            .Select(m => new { type = "milk", date = m.CreatedAt, description = $"Milk log: {m.Goat.Name} — {m.Amount} lbs" })
            .ToListAsync();

        var recentSales = await _db.Sales.Include(s => s.Customer).OrderByDescending(s => s.CreatedAt).Take(5)
            .Select(s => new { type = "sale", date = s.CreatedAt, description = $"Sale: {s.Description} — ${s.Amount}" })
            .ToListAsync();

        var feed = recentGoats
            .Concat(recentMedical)
            .Concat(recentMilk)
            .Concat(recentSales)
            .OrderByDescending(a => a.date)
            .Take(count);

        return Ok(feed);
    }

    // --- Dashboard Alerts ---

    [HttpGet("alerts")]
    public async Task<ActionResult<object>> GetAlerts()
    {
        var overdueMeds = await _db.MedicalRecords
            .Include(r => r.Goat)
            .Where(r => r.NextDueDate != null && r.NextDueDate <= DateTime.UtcNow)
            .CountAsync();

        var upcomingDueDates = await _db.BreedingRecords
            .Where(b => b.Outcome == BreedingOutcome.Confirmed && b.EstimatedDueDate != null
                && b.EstimatedDueDate <= DateTime.UtcNow.AddDays(14) && b.EstimatedDueDate > DateTime.UtcNow)
            .CountAsync();

        var lowFeedStock = await _db.FeedInventory
            .Where(f => f.LowStockThreshold != null && f.QuantityOnHand <= f.LowStockThreshold)
            .CountAsync();

        var expiringMeds = await _db.MedicineCabinetItems
            .Where(c => c.ExpirationDate != null && c.ExpirationDate <= DateTime.UtcNow.AddDays(30))
            .CountAsync();

        var sickGoats = await _db.Goats.CountAsync(g => g.Status == GoatStatus.Sick || g.Status == GoatStatus.AtVet);

        return Ok(new { overdueMedications = overdueMeds, upcomingDueDates, lowFeedStock, expiringMedications = expiringMeds, sickGoats });
    }

    private static string ToCsv<T>(IEnumerable<T> items)
    {
        var props = typeof(T).GetProperties();
        var header = string.Join(",", props.Select(p => p.Name));
        var rows = items.Select(item =>
            string.Join(",", props.Select(p =>
            {
                var val = p.GetValue(item)?.ToString() ?? "";
                return val.Contains(',') || val.Contains('"') || val.Contains('\n')
                    ? $"\"{val.Replace("\"", "\"\"")}\""
                    : val;
            })));
        return header + "\n" + string.Join("\n", rows);
    }
}
