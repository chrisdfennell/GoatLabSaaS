using GoatLab.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Pedigree;

public record CoiResult(double Coi, IReadOnlyList<CommonAncestor> CommonAncestors);

public record CommonAncestor(int GoatId, string? Name, double Contribution, int SirePathLength, int DamPathLength);

// Wright's coefficient of inbreeding (Fx) for a goat or hypothetical mating.
//
// Formula: F = Σ over each common ancestor A of (1/2)^(n1+n2+1) * (1+F_A),
// where n1 = number of links from sire to A and n2 = number of links from dam
// to A. Each distinct ancestor path counts; F_A is the COI of A itself.
//
// Walks pedigree to MaxDepth generations from each parent (so the deepest
// shared ancestor we'll detect is great-great-great-great-great-grandparent
// at n=6). Inbreeding contributions beyond that are <0.05% and dominated by
// noise in the source data.
public class CoiCalculator
{
    private const int MaxDepth = 6;

    private readonly GoatLabDbContext _db;
    public CoiCalculator(GoatLabDbContext db) => _db = db;

    public async Task<CoiResult?> ComputeAsync(int goatId, CancellationToken ct = default)
    {
        var goat = await _db.Goats
            .Where(g => g.Id == goatId)
            .Select(g => new { g.SireId, g.DamId, g.TenantId })
            .FirstOrDefaultAsync(ct);
        if (goat is null) return null;
        if (goat.SireId is null || goat.DamId is null)
            return new CoiResult(0.0, Array.Empty<CommonAncestor>());

        return await ComputeCoreAsync(goat.SireId.Value, goat.DamId.Value, goat.TenantId, ct);
    }

    public async Task<CoiResult?> ComputeForMateAsync(int sireId, int damId, CancellationToken ct = default)
    {
        var sire = await _db.Goats
            .Where(g => g.Id == sireId)
            .Select(g => new { g.TenantId })
            .FirstOrDefaultAsync(ct);
        if (sire is null) return null;

        return await ComputeCoreAsync(sireId, damId, sire.TenantId, ct);
    }

    private async Task<CoiResult> ComputeCoreAsync(int sireId, int damId, int tenantId, CancellationToken ct)
    {
        var lookup = await _db.Goats
            .Where(g => g.TenantId == tenantId)
            .Select(g => new { g.Id, g.SireId, g.DamId, g.Name })
            .ToDictionaryAsync(
                g => g.Id,
                g => new ParentInfo(g.SireId, g.DamId, g.Name),
                ct);

        var sireAncestors = GetAncestorPaths(sireId, lookup);
        var damAncestors = GetAncestorPaths(damId, lookup);

        var coiCache = new Dictionary<int, double>();
        var inProgress = new HashSet<int>();
        var commons = new List<CommonAncestor>();
        double total = 0;

        foreach (var (ancestorId, sirePaths) in sireAncestors)
        {
            if (!damAncestors.TryGetValue(ancestorId, out var damPaths)) continue;

            var fA = ComputeFor(ancestorId, lookup, coiCache, inProgress);
            double contrib = 0;
            int minN1 = int.MaxValue, minN2 = int.MaxValue;

            foreach (var n1 in sirePaths)
            foreach (var n2 in damPaths)
            {
                contrib += Math.Pow(0.5, n1 + n2 + 1) * (1 + fA);
                if (n1 < minN1) minN1 = n1;
                if (n2 < minN2) minN2 = n2;
            }

            total += contrib;
            lookup.TryGetValue(ancestorId, out var info);
            commons.Add(new CommonAncestor(ancestorId, info?.Name, contrib, minN1, minN2));
        }

        return new CoiResult(total, commons.OrderByDescending(c => c.Contribution).ToList());
    }

    private static double ComputeFor(
        int goatId,
        Dictionary<int, ParentInfo> lookup,
        Dictionary<int, double> cache,
        HashSet<int> inProgress)
    {
        if (cache.TryGetValue(goatId, out var cached)) return cached;
        if (!inProgress.Add(goatId)) return 0;
        try
        {
            if (!lookup.TryGetValue(goatId, out var info)) { cache[goatId] = 0; return 0; }
            if (info.SireId is null || info.DamId is null) { cache[goatId] = 0; return 0; }

            var sireAnc = GetAncestorPaths(info.SireId.Value, lookup);
            var damAnc = GetAncestorPaths(info.DamId.Value, lookup);

            double total = 0;
            foreach (var (ancestorId, sPaths) in sireAnc)
            {
                if (!damAnc.TryGetValue(ancestorId, out var dPaths)) continue;
                var fA = ComputeFor(ancestorId, lookup, cache, inProgress);
                foreach (var n1 in sPaths)
                foreach (var n2 in dPaths)
                {
                    total += Math.Pow(0.5, n1 + n2 + 1) * (1 + fA);
                }
            }
            cache[goatId] = total;
            return total;
        }
        finally { inProgress.Remove(goatId); }
    }

    private static Dictionary<int, List<int>> GetAncestorPaths(int rootGoatId, Dictionary<int, ParentInfo> lookup)
    {
        var result = new Dictionary<int, List<int>>();
        Recurse(rootGoatId, 0);
        return result;

        void Recurse(int id, int depth)
        {
            if (depth > MaxDepth) return;
            if (!result.TryGetValue(id, out var paths))
            {
                paths = new List<int>();
                result[id] = paths;
            }
            paths.Add(depth);

            if (!lookup.TryGetValue(id, out var info)) return;
            if (info.SireId.HasValue) Recurse(info.SireId.Value, depth + 1);
            if (info.DamId.HasValue) Recurse(info.DamId.Value, depth + 1);
        }
    }

    private record ParentInfo(int? SireId, int? DamId, string? Name);
}
