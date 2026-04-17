using GoatLab.Server.Data;
using GoatLab.Server.Services.Pdf.Templates;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;

namespace GoatLab.Server.Services.Pdf;

// Generates downloadable PDFs from existing tenant data. Each method returns
// the bytes (controller wraps in File(...)) and null if the source row isn't
// found. Tenant scoping is enforced by the regular EF query filter — no
// IgnoreQueryFilters here.
public class PdfService
{
    private readonly GoatLabDbContext _db;

    public PdfService(GoatLabDbContext db) => _db = db;

    public async Task<byte[]?> GeneratePedigreeAsync(int goatId, string tenantName, CancellationToken cancellationToken = default)
    {
        var goat = await _db.Goats
            .Include(g => g.Sire).ThenInclude(s => s!.Sire).ThenInclude(ss => ss!.Sire)
            .Include(g => g.Sire).ThenInclude(s => s!.Sire).ThenInclude(ss => ss!.Dam)
            .Include(g => g.Sire).ThenInclude(s => s!.Dam).ThenInclude(sd => sd!.Sire)
            .Include(g => g.Sire).ThenInclude(s => s!.Dam).ThenInclude(sd => sd!.Dam)
            .Include(g => g.Dam).ThenInclude(d => d!.Sire).ThenInclude(ds => ds!.Sire)
            .Include(g => g.Dam).ThenInclude(d => d!.Sire).ThenInclude(ds => ds!.Dam)
            .Include(g => g.Dam).ThenInclude(d => d!.Dam).ThenInclude(dd => dd!.Sire)
            .Include(g => g.Dam).ThenInclude(d => d!.Dam).ThenInclude(dd => dd!.Dam)
            .FirstOrDefaultAsync(g => g.Id == goatId, cancellationToken);
        if (goat is null) return null;

        return new PedigreeDocument(goat, tenantName).GeneratePdf();
    }

    public async Task<byte[]?> GenerateSalesContractAsync(int saleId, string tenantName, CancellationToken cancellationToken = default)
    {
        var sale = await _db.Sales
            .Include(s => s.Customer)
            .Include(s => s.Goat)
            .FirstOrDefaultAsync(s => s.Id == saleId, cancellationToken);
        if (sale is null) return null;

        return new SalesContractDocument(sale, tenantName).GeneratePdf();
    }

    public async Task<byte[]?> GenerateHealthCertificateAsync(int goatId, string tenantName, CancellationToken cancellationToken = default)
    {
        var goat = await _db.Goats.FirstOrDefaultAsync(g => g.Id == goatId, cancellationToken);
        if (goat is null) return null;

        var since = DateTime.UtcNow.AddMonths(-12);
        var vaccinations = await _db.MedicalRecords
            .Include(r => r.Medication)
            .Where(r => r.GoatId == goatId
                     && r.RecordType == MedicalRecordType.Vaccination
                     && r.Date >= since)
            .OrderByDescending(r => r.Date)
            .ToListAsync(cancellationToken);

        var latestWeight = await _db.WeightRecords
            .Where(w => w.GoatId == goatId)
            .OrderByDescending(w => w.Date)
            .FirstOrDefaultAsync(cancellationToken);

        var latestFamacha = await _db.FamachaScores
            .Where(f => f.GoatId == goatId)
            .OrderByDescending(f => f.Date)
            .FirstOrDefaultAsync(cancellationToken);

        return new HealthCertificateDocument(goat, vaccinations, latestWeight, latestFamacha, tenantName).GeneratePdf();
    }

    /// <summary>Caller-side helper: load the current tenant's display name once.</summary>
    public async Task<string> GetTenantNameAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        var name = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return name ?? "GoatLab";
    }
}
