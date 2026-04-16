using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using GoatLab.Server.Data;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services;

// Imports goats from an ADGA/AGS registry CSV export. Key behaviors:
//
// - Upserts by RegistrationNumber: if a goat with that reg# already exists in
//   the tenant, updates it; otherwise creates new.
// - Pedigree linking: looks up sire/dam by registration number. If a parent
//   isn't in the herd yet, creates a minimal "external" stub so the pedigree
//   chain isn't broken. Those stubs can be upgraded to full records later via
//   another import or manual entry.
// - Sets GoatRegistry enum from the registry column or auto-detects from the
//   registration number prefix (ADGA: letter prefix like "D1234567", "N1234567").
public class RegistryImportService
{
    private readonly GoatLabDbContext _db;

    public RegistryImportService(GoatLabDbContext db) => _db = db;

    public record ImportResult(int Created, int Updated, int Skipped, List<ImportError> Errors);
    public record ImportError(int Row, string Message);

    public async Task<ImportResult> ImportAsync(Stream csvStream, int tenantId, CancellationToken ct)
    {
        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            PrepareHeaderForMatch = args => args.Header.Trim().Replace("_", "").Replace(" ", "").ToLowerInvariant(),
            MissingFieldFound = null,
            BadDataFound = null,
        };

        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, cfg);

        try
        {
            await csv.ReadAsync();
            csv.ReadHeader();
        }
        catch (Exception ex)
        {
            return new ImportResult(0, 0, 0, new() { new(0, $"Couldn't read CSV header: {ex.Message}") });
        }

        // Pre-load existing goats by registration number for fast upsert lookups.
        var existingByReg = await _db.Goats
            .Where(g => g.TenantId == tenantId && g.RegistrationNumber != null)
            .ToDictionaryAsync(g => g.RegistrationNumber!, ct);

        int created = 0, updated = 0, skipped = 0;
        var errors = new List<ImportError>();
        var pendingParents = new Dictionary<string, Goat>(StringComparer.OrdinalIgnoreCase);
        int row = 1;

        while (await csv.ReadAsync())
        {
            row++;
            try
            {
                var regNum = Field(csv, "registrationnumber", "regnumber", "registration", "reg");
                var name = Field(csv, "name", "animalname", "goatname");

                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(regNum))
                {
                    skipped++;
                    continue;
                }

                var breed = Field(csv, "breed", "breedname");
                var sexStr = Field(csv, "sex", "gender");
                var dobStr = Field(csv, "dateofbirth", "dob", "birthdate");
                var tattooL = Field(csv, "tattooleft", "lefttattoo", "leftear");
                var tattooR = Field(csv, "tattooright", "righttattoo", "rightear");
                var color = Field(csv, "color", "colordescription", "markings");
                var statusStr = Field(csv, "status");
                var registryStr = Field(csv, "registry");
                var sireReg = Field(csv, "sireregistration", "sirereg", "sireregno");
                var sireName = Field(csv, "sirename", "sire");
                var damReg = Field(csv, "damregistration", "damreg", "damregno");
                var damName = Field(csv, "damname", "dam");
                var breederName = Field(csv, "breedername", "breeder");

                var gender = ParseGender(sexStr);
                var dob = ParseDate(dobStr);
                var status = ParseStatus(statusStr);
                var registry = ParseRegistry(registryStr, regNum);

                Goat goat;
                bool isNew;
                if (!string.IsNullOrWhiteSpace(regNum) && existingByReg.TryGetValue(regNum, out var existing))
                {
                    goat = existing;
                    isNew = false;
                }
                else
                {
                    goat = new Goat { TenantId = tenantId, CreatedAt = DateTime.UtcNow };
                    isNew = true;
                }

                if (!string.IsNullOrWhiteSpace(name)) goat.Name = name.Trim();
                if (!string.IsNullOrWhiteSpace(regNum)) goat.RegistrationNumber = regNum.Trim();
                if (!string.IsNullOrWhiteSpace(breed)) goat.Breed = breed.Trim();
                if (gender.HasValue) goat.Gender = gender.Value;
                if (dob.HasValue) goat.DateOfBirth = dob;
                if (!string.IsNullOrWhiteSpace(tattooL)) goat.TattooLeft = tattooL.Trim();
                if (!string.IsNullOrWhiteSpace(tattooR)) goat.TattooRight = tattooR.Trim();
                if (!string.IsNullOrWhiteSpace(color)) goat.Bio = string.IsNullOrWhiteSpace(goat.Bio)
                    ? $"Color: {color.Trim()}" : goat.Bio;
                if (status.HasValue) goat.Status = status.Value;
                if (registry != GoatRegistry.None) goat.Registry = registry;
                if (!string.IsNullOrWhiteSpace(breederName)) goat.BreederName = breederName.Trim();
                goat.UpdatedAt = DateTime.UtcNow;

                // Pedigree linking — resolve sire/dam by registration number
                goat.SireId = await ResolveParentAsync(sireReg, sireName, Gender.Male, tenantId, existingByReg, pendingParents, ct);
                goat.DamId = await ResolveParentAsync(damReg, damName, Gender.Female, tenantId, existingByReg, pendingParents, ct);

                if (isNew)
                {
                    _db.Goats.Add(goat);
                    if (!string.IsNullOrWhiteSpace(regNum))
                        existingByReg[regNum] = goat;
                    created++;
                }
                else
                {
                    updated++;
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ImportError(row, ex.Message));
            }
        }

        await _db.SaveChangesAsync(ct);
        return new ImportResult(created, updated, skipped, errors);
    }

    private async Task<int?> ResolveParentAsync(
        string? regNum, string? name, Gender gender, int tenantId,
        Dictionary<string, Goat> cache, Dictionary<string, Goat> pending,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(regNum) && string.IsNullOrWhiteSpace(name))
            return null;

        var key = regNum?.Trim().ToUpperInvariant() ?? name!.Trim().ToUpperInvariant();

        // Already in the herd?
        if (!string.IsNullOrWhiteSpace(regNum) && cache.TryGetValue(regNum.Trim(), out var existing))
            return existing.Id != 0 ? existing.Id : null;

        // Already created as a stub in this import pass?
        if (pending.TryGetValue(key, out var stub))
            return stub.Id != 0 ? stub.Id : null;

        // Create an external stub — minimal record so pedigree chain isn't broken.
        var parentStub = new Goat
        {
            TenantId = tenantId,
            Name = !string.IsNullOrWhiteSpace(name) ? name.Trim() : $"[{regNum?.Trim()}]",
            RegistrationNumber = string.IsNullOrWhiteSpace(regNum) ? null : regNum.Trim(),
            Gender = gender,
            IsExternal = true,
            Registry = ParseRegistry(null, regNum),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Goats.Add(parentStub);
        await _db.SaveChangesAsync(ct);

        pending[key] = parentStub;
        if (!string.IsNullOrWhiteSpace(regNum))
            cache[regNum.Trim()] = parentStub;

        return parentStub.Id;
    }

    private static string? Field(CsvReader csv, params string[] candidates)
    {
        foreach (var name in candidates)
        {
            if (csv.TryGetField<string>(name, out var val) && !string.IsNullOrWhiteSpace(val))
                return val;
        }
        return null;
    }

    private static Gender? ParseGender(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "doe" or "female" or "f" => Gender.Female,
        "buck" or "male" or "m" => Gender.Male,
        "wether" or "w" => Gender.Wether,
        _ => null,
    };

    private static DateTime? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;
        return null;
    }

    private static GoatStatus? ParseStatus(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "active" or "alive" or "healthy" or "dry" or "lactating" or "milking"
            or "pregnant" or "bred" or "kidding" or "retired" => GoatStatus.Healthy,
        "sold" => GoatStatus.Sold,
        "dead" or "deceased" => GoatStatus.Deceased,
        "sick" or "ill" => GoatStatus.Sick,
        "atvet" or "at vet" or "vet" => GoatStatus.AtVet,
        _ => null,
    };

    private static GoatRegistry ParseRegistry(string? explicit_, string? regNum)
    {
        if (!string.IsNullOrWhiteSpace(explicit_))
        {
            if (explicit_.Contains("ADGA", StringComparison.OrdinalIgnoreCase)) return GoatRegistry.ADGA;
            if (explicit_.Contains("AGS", StringComparison.OrdinalIgnoreCase)) return GoatRegistry.AGS;
        }
        // Auto-detect from registration number prefix: ADGA uses letter + digits (e.g., "D1234567", "AN1234567")
        if (!string.IsNullOrWhiteSpace(regNum))
        {
            var trimmed = regNum.Trim();
            if (trimmed.Length >= 2 && char.IsLetter(trimmed[0]) && char.IsDigit(trimmed[^1]))
                return GoatRegistry.ADGA;
        }
        return GoatRegistry.None;
    }
}
