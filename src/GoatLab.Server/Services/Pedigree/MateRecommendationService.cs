using GoatLab.Server.Data;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Pedigree;

// Ranks every non-wether male goat in the tenant as a potential mate for
// <paramref name="doeId"/> using three signals already present in the database:
//   1. Projected offspring COI (via CoiCalculator — lower is better).
//   2. Proven reproduction from prior kiddings (avg litter size + live-birth rate).
//   3. Daughter-milk performance (lifetime daily-avg lbs across female offspring).
// The composite score is 0–100 with COI contributing up to 70 and production
// signals up to 30. Unproven bucks (no kiddings yet) keep their COI score but
// carry a "no kidding history" rationale so the owner can weigh them honestly.

public class MateRecommendationService
{
    private readonly GoatLabDbContext _db;
    private readonly CoiCalculator _coi;

    public MateRecommendationService(GoatLabDbContext db, CoiCalculator coi)
    {
        _db = db;
        _coi = coi;
    }

    public async Task<IReadOnlyList<MateRecommendationDto>?> RecommendForDoeAsync(
        int doeId,
        int limit = 10,
        CancellationToken ct = default)
    {
        var doe = await _db.Goats
            .Where(g => g.Id == doeId)
            .Select(g => new { g.Id, g.TenantId, g.Gender, g.IsExternal })
            .FirstOrDefaultAsync(ct);
        if (doe is null || doe.IsExternal || doe.Gender != Gender.Female) return null;

        // Candidate bucks: in the same tenant, intact male, not external, not
        // the doe's own sire/dam (can't happen — she's female — but guard anyway).
        var bucks = await _db.Goats
            .Where(g => g.TenantId == doe.TenantId
                        && g.Gender == Gender.Male
                        && !g.IsExternal
                        && g.Status != GoatStatus.Deceased
                        && g.Status != GoatStatus.Sold)
            .Select(g => new { g.Id, g.Name, g.EarTag, g.Breed, g.DateOfBirth })
            .ToListAsync(ct);
        if (bucks.Count == 0) return Array.Empty<MateRecommendationDto>();

        var buckIds = bucks.Select(b => b.Id).ToList();

        // Reproductive history (all-time, not windowed — we're scoring lifetime performance).
        // KiddingRecord → BreedingRecord.BuckId (nullable, so filter first).
        var kiddingAgg = await _db.KiddingRecords
            .Where(k => k.BreedingRecord.BuckId != null
                        && buckIds.Contains(k.BreedingRecord.BuckId!.Value))
            .GroupBy(k => k.BreedingRecord.BuckId!.Value)
            .Select(g => new
            {
                BuckId = g.Key,
                Kiddings = g.Count(),
                Born = g.Sum(x => x.KidsBorn),
                Alive = g.Sum(x => x.KidsAlive),
            })
            .ToDictionaryAsync(g => g.BuckId, ct);

        // Daughter milk: female offspring of each buck, total lifetime milk and
        // distinct milking-day count. Two queries keyed by the buck id set.
        var daughters = await _db.Goats
            .Where(g => g.Gender == Gender.Female
                        && g.SireId != null
                        && buckIds.Contains(g.SireId.Value))
            .Select(g => new { g.Id, g.SireId })
            .ToListAsync(ct);

        var daughterIds = daughters.Select(d => d.Id).ToList();
        var milkRaw = daughterIds.Count == 0
            ? new List<(int GoatId, double Amount, DateTime Day)>()
            : (await _db.MilkLogs
                .Where(m => daughterIds.Contains(m.GoatId))
                .Select(m => new { m.GoatId, m.Amount, Day = m.Date.Date })
                .ToListAsync(ct))
                .Select(m => (m.GoatId, m.Amount, m.Day))
                .ToList();

        var milkByBuck = new Dictionary<int, (double TotalLbs, int Days)>();
        foreach (var group in daughters.Where(d => d.SireId.HasValue).GroupBy(d => d.SireId!.Value))
        {
            var ids = group.Select(x => x.Id).ToHashSet();
            var rows = milkRaw.Where(r => ids.Contains(r.GoatId)).ToList();
            if (rows.Count == 0) continue;
            var total = rows.Sum(r => r.Amount);
            var days = rows.Select(r => (r.GoatId, r.Day)).Distinct().Count();
            if (days > 0) milkByBuck[group.Key] = (total, days);
        }

        var offspringCountByBuck = daughters
            .Where(d => d.SireId.HasValue)
            .GroupBy(d => d.SireId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        // Also count male offspring for the "offspring" headline.
        var allOffspringCounts = await _db.Goats
            .Where(g => g.SireId != null && buckIds.Contains(g.SireId.Value))
            .GroupBy(g => g.SireId!.Value)
            .Select(g => new { BuckId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.BuckId, g => g.Count, ct);

        var results = new List<MateRecommendationDto>(bucks.Count);
        foreach (var buck in bucks)
        {
            var coiResult = await _coi.ComputeForMateAsync(buck.Id, doeId, ct);
            var coi = coiResult?.Coi ?? 0;

            kiddingAgg.TryGetValue(buck.Id, out var kagg);
            double? avgLitter = kagg is { Kiddings: > 0 } ? (double)kagg.Born / kagg.Kiddings : null;
            double? liveBirthRate = kagg is { Born: > 0 } ? (double)kagg.Alive / kagg.Born : null;

            double? daughterMilkDaily = milkByBuck.TryGetValue(buck.Id, out var milk) && milk.Days > 0
                ? milk.TotalLbs / milk.Days
                : null;

            var (score, rationale) = Score(
                coi,
                kiddingsSired: kagg?.Kiddings ?? 0,
                avgLitter,
                liveBirthRate,
                daughterMilkDaily);

            results.Add(new MateRecommendationDto(
                buck.Id,
                buck.Name,
                buck.EarTag,
                buck.Breed,
                buck.DateOfBirth,
                coi,
                kagg?.Kiddings ?? 0,
                allOffspringCounts.GetValueOrDefault(buck.Id, 0),
                avgLitter,
                liveBirthRate,
                daughterMilkDaily,
                score,
                rationale));
        }

        return results
            .OrderByDescending(r => r.CompositeScore)
            .ThenBy(r => r.ProjectedCoi)
            .Take(limit)
            .ToList();
    }

    // Composite: 0..70 from COI, plus 0..30 from production signals.
    // Dairy milk target is 5 lb/day — generous enough for meat/fiber breeds not
    // to be penalized; bucks whose daughters beat that get the full 10 pts.
    private const double DairyMilkTargetLbsPerDay = 5.0;

    private static (double Score, string Rationale) Score(
        double coi,
        int kiddingsSired,
        double? avgLitter,
        double? liveBirthRate,
        double? daughterMilkDaily)
    {
        // coi=0 → 70; coi=0.0625 (safe ceiling) → 52.5; coi=0.25 → 0.
        var coiScore = Math.Max(0, 70 - coi * 280);

        double production = 0;
        var notes = new List<string>();

        notes.Add($"Projected COI {coi * 100:F2}%");

        if (kiddingsSired > 0 && liveBirthRate.HasValue)
        {
            // 85% → 0 pts, 100% → 10 pts.
            var lbrScore = 10 * Math.Clamp((liveBirthRate.Value - 0.85) / 0.15, 0, 1);
            production += lbrScore;
            notes.Add($"{liveBirthRate.Value * 100:F0}% live-birth rate across {kiddingsSired} kidding(s)");
        }

        if (kiddingsSired > 0 && avgLitter.HasValue)
        {
            // 1.0 → 0 pts, 2.0+ → 10 pts.
            var litterScore = 10 * Math.Clamp(avgLitter.Value - 1.0, 0, 1);
            production += litterScore;
            notes.Add($"avg litter {avgLitter.Value:F1}");
        }

        if (daughterMilkDaily.HasValue)
        {
            // target 5 lb/day; daughters above that hit full 10 pts.
            var milkScore = 10 * Math.Clamp(daughterMilkDaily.Value / DairyMilkTargetLbsPerDay, 0, 1);
            production += milkScore;
            notes.Add($"daughters average {daughterMilkDaily.Value:F1} lb/day milk");
        }

        if (kiddingsSired == 0)
        {
            notes.Add("unproven — no kidding history yet");
        }

        var composite = Math.Min(100, coiScore + production);
        return (composite, string.Join(" · ", notes));
    }
}
