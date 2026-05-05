using GoatLab.Server.Data;
using GoatLab.Server.Services;
using GoatLab.Shared.Models;
using GoatLab.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Cross-farm public lineage page. Walks a goat's pedigree to depth 4 and
// links ancestors that exist on OTHER public farms (matched by registration
// number) so a buyer can traverse provenance across the GoatLab network —
// the kind of graph no single-farm app can produce.
//
// Visibility rules for the focal goat (any one is enough):
//   * Currently for sale on a public-profile farm
//   * Has been the subject of an accepted GoatTransfer (proves ownership trail)
//   * Has at least one offspring that is currently public-listed (lets buyers
//     trace back from a kid they're considering to its sire / dam)
//
// Pedigree links to OTHER goats are only emitted when the matched goat lives
// on a public-profile, non-deleted, non-suspended tenant. Private homestead
// data never leaks; reg numbers shown without a link are read-only attribution.
[AllowAnonymous]
[RequiresSaas]
public class LineagePagesController : Controller
{
    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;

    public LineagePagesController(GoatLabDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public class LineageVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Breed { get; set; }
        public string Gender { get; set; } = "";
        public DateTime? DateOfBirth { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? Registry { get; set; }
        public string? FarmName { get; set; }
        public string? FarmSlug { get; set; }
        public bool FarmIsPublic { get; set; }
        public bool IsCurrentlyListed { get; set; }
        public string? PrimaryPhotoUrl { get; set; }
        public PedigreeNode? Pedigree { get; set; }
        public List<TransferStop> TransferHistory { get; set; } = new();
    }

    public record PedigreeNode(
        int? Id,
        string Name,
        string? RegistrationNumber,
        string? Breed,
        // CrossFarmId is set when this ancestor exists on a public farm under
        // its registration number — the cell renders as a clickable link.
        int? CrossFarmId,
        string? CrossFarmName,
        PedigreeNode? Sire,
        PedigreeNode? Dam);

    public record TransferStop(string FarmName, string? FarmSlug, DateTime At);

    [HttpGet("/lineage/{goatId:int}")]
    [ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Show(int goatId, CancellationToken ct)
    {
        // Anonymous request: no tenant claim. Bypass the tenant filter; we
        // enforce visibility manually below.
        _tenantContext.BypassFilter = true;

        var focal = await _db.Goats.IgnoreQueryFilters()
            .Include(g => g.Tenant)
            .Include(g => g.Photos)
            .Include(g => g.Sire).ThenInclude(s => s!.Sire)
            .Include(g => g.Sire).ThenInclude(s => s!.Dam)
            .Include(g => g.Dam).ThenInclude(d => d!.Sire)
            .Include(g => g.Dam).ThenInclude(d => d!.Dam)
            .FirstOrDefaultAsync(g => g.Id == goatId, ct);

        if (focal is null) return NotFound();

        // Visibility gate.
        var farmIsPublic = focal.Tenant?.PublicProfileEnabled == true
            && focal.Tenant.DeletedAt == null
            && focal.Tenant.SuspendedAt == null;
        var isListed = focal.IsListedForSale && !focal.IsExternal && farmIsPublic;
        var hadTransfer = await _db.GoatTransfers.IgnoreQueryFilters()
            .AnyAsync(t => t.GoatId == focal.Id && t.AcceptedAt != null, ct);
        var hasPublicOffspring = await _db.Goats.IgnoreQueryFilters()
            .AnyAsync(c => (c.SireId == focal.Id || c.DamId == focal.Id)
                           && c.IsListedForSale && !c.IsExternal
                           && c.Tenant!.PublicProfileEnabled
                           && c.Tenant.DeletedAt == null
                           && c.Tenant.SuspendedAt == null, ct);
        if (!isListed && !hadTransfer && !hasPublicOffspring)
        {
            Response.StatusCode = 404;
            return View("LineageNotPublic");
        }

        // Collect every registration number we'll need cross-farm matches for.
        var regs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Walk(focal, depth: 0, depthLimit: 4, action: g =>
        {
            if (!string.IsNullOrWhiteSpace(g.RegistrationNumber))
                regs.Add(g.RegistrationNumber!);
        });
        regs.Remove(focal.RegistrationNumber ?? "__missing__"); // focal is always self

        var crossFarm = new Dictionary<string, (int Id, string FarmName)>(StringComparer.OrdinalIgnoreCase);
        if (regs.Count > 0)
        {
            // Pick the most-recently-updated public goat per registration
            // number. Keeps current ownership in front of stale records.
            var matches = await _db.Goats.IgnoreQueryFilters()
                .Where(g => regs.Contains(g.RegistrationNumber!) && !g.IsExternal
                            && g.Tenant!.PublicProfileEnabled
                            && g.Tenant.DeletedAt == null
                            && g.Tenant.SuspendedAt == null
                            && g.Id != focal.Id)
                .OrderByDescending(g => g.UpdatedAt)
                .Select(g => new { g.Id, g.RegistrationNumber, FarmName = g.Tenant!.Name })
                .ToListAsync(ct);
            foreach (var m in matches)
            {
                if (string.IsNullOrEmpty(m.RegistrationNumber)) continue;
                crossFarm.TryAdd(m.RegistrationNumber, (m.Id, m.FarmName));
            }
        }

        // Build pedigree tree with cross-farm links injected.
        PedigreeNode? Build(Goat? g)
        {
            if (g is null) return null;
            int? cfId = null; string? cfName = null;
            if (!string.IsNullOrWhiteSpace(g.RegistrationNumber)
                && g.Id != focal.Id
                && crossFarm.TryGetValue(g.RegistrationNumber!, out var hit))
            {
                cfId = hit.Id; cfName = hit.FarmName;
            }
            return new PedigreeNode(g.Id, g.Name, g.RegistrationNumber, g.Breed,
                cfId, cfName, Build(g.Sire), Build(g.Dam));
        }

        // Recent transfer history: where has this goat lived?
        var transfers = await _db.GoatTransfers.IgnoreQueryFilters()
            .Where(t => t.GoatId == focal.Id && t.AcceptedAt != null)
            .OrderByDescending(t => t.AcceptedAt)
            .Take(8)
            .Select(t => new { t.AcceptedAt, FromTenantId = t.FromTenantId })
            .ToListAsync(ct);
        var fromTenantIds = transfers.Select(t => t.FromTenantId).Distinct().ToList();
        var farms = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => fromTenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name, t.Slug, t.PublicProfileEnabled, IsActive = t.DeletedAt == null && t.SuspendedAt == null })
            .ToDictionaryAsync(t => t.Id, ct);
        var transferStops = transfers
            .Where(t => t.AcceptedAt.HasValue && farms.ContainsKey(t.FromTenantId))
            .Select(t =>
            {
                var f = farms[t.FromTenantId];
                return new TransferStop(
                    FarmName: f.Name,
                    FarmSlug: (f.PublicProfileEnabled && f.IsActive) ? f.Slug : null,
                    At: t.AcceptedAt!.Value);
            })
            .ToList();

        var primaryPhoto = focal.Photos
            .OrderByDescending(p => p.IsPrimary)
            .ThenBy(p => p.UploadedAt)
            .Select(p => p.FilePath)
            .FirstOrDefault();

        var vm = new LineageVm
        {
            Id = focal.Id,
            Name = focal.Name,
            Breed = focal.Breed,
            Gender = focal.Gender.ToString(),
            DateOfBirth = focal.DateOfBirth,
            RegistrationNumber = focal.RegistrationNumber,
            Registry = focal.Registry == GoatRegistry.None ? null : focal.Registry.ToString(),
            FarmName = focal.Tenant?.Name,
            FarmSlug = farmIsPublic ? focal.Tenant?.Slug : null,
            FarmIsPublic = farmIsPublic,
            IsCurrentlyListed = isListed,
            PrimaryPhotoUrl = string.IsNullOrEmpty(primaryPhoto) ? null : "/" + primaryPhoto,
            Pedigree = Build(focal),
            TransferHistory = transferStops,
        };

        return View(vm);
    }

    private static void Walk(Goat? g, int depth, int depthLimit, Action<Goat> action)
    {
        if (g is null || depth > depthLimit) return;
        action(g);
        Walk(g.Sire, depth + 1, depthLimit, action);
        Walk(g.Dam, depth + 1, depthLimit, action);
    }
}
