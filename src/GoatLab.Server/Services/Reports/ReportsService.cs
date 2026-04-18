using GoatLab.Server.Data;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Reports;

// All reports run under the tenant query filter on GoatLabDbContext. The
// controller layer handles feature gating via [RequiresFeature].
//
// Date handling: all endpoints accept inclusive From/To at UTC-midnight. The
// service shifts To forward by one day internally to turn the inclusive upper
// bound into an exclusive "< end" clause that's safe against time-of-day data.
public class ReportsService
{
    private readonly GoatLabDbContext _db;
    public ReportsService(GoatLabDbContext db) => _db = db;

    // ---------- helpers ----------

    private static (DateTime start, DateTime endExclusive) Window(DateTime from, DateTime to)
    {
        var start = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc);
        return (start, end);
    }

    // ---------- P&L ----------

    public async Task<PnlReportDto> GetPnlAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var (start, end) = Window(from, to);
        var txns = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.Date >= start && t.Date < end)
            .Select(t => new { t.Type, t.Amount, t.Category, t.Date, t.GoatId })
            .ToListAsync(ct);

        decimal income = txns.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        decimal expenses = txns.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

        var incomeByCat = txns.Where(t => t.Type == TransactionType.Income)
            .GroupBy(t => t.Category ?? "Uncategorized")
            .Select(g => new CategoryTotalDto(g.Key, g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Total)
            .ToList();

        var expensesByCat = txns.Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => t.Category ?? "Uncategorized")
            .Select(g => new CategoryTotalDto(g.Key, g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Total)
            .ToList();

        var monthly = txns
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g =>
            {
                var inc = g.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
                var exp = g.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);
                return new MonthlyPnlDto(g.Key.Year, g.Key.Month, inc, exp, inc - exp);
            })
            .ToList();

        var goatIds = txns.Where(t => t.GoatId.HasValue).Select(t => t.GoatId!.Value).Distinct().ToList();
        var goatNames = goatIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Goats.AsNoTracking()
                .Where(g => goatIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name })
                .ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        var costPerGoat = txns.Where(t => t.GoatId.HasValue)
            .GroupBy(t => t.GoatId!.Value)
            .Select(g =>
            {
                var inc = g.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
                var exp = g.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);
                goatNames.TryGetValue(g.Key, out var name);
                return new GoatPnlRowDto(g.Key, name ?? $"Goat #{g.Key}", inc, exp, inc - exp);
            })
            .OrderBy(r => r.Net)
            .ToList();

        return new PnlReportDto(
            new ReportWindowDto(start, end.AddDays(-1)),
            income, expenses, income - expenses,
            incomeByCat, expensesByCat, monthly, costPerGoat);
    }

    // ---------- Milk trends ----------

    public async Task<MilkTrendsReportDto> GetMilkTrendsAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var (start, end) = Window(from, to);
        var logs = await _db.MilkLogs
            .AsNoTracking()
            .Where(m => m.Date >= start && m.Date < end)
            .Select(m => new { m.GoatId, m.Date, m.Amount })
            .ToListAsync(ct);

        double total = logs.Sum(l => l.Amount);
        int dayCount = Math.Max(1, (int)(end - start).TotalDays);
        double dailyAvg = total / dayCount;

        var daily = logs
            .GroupBy(l => l.Date.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyTotalDto(g.Key, g.Sum(x => x.Amount)))
            .ToList();

        var goatIds = logs.Select(l => l.GoatId).Distinct().ToList();
        var goatNames = goatIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Goats.AsNoTracking()
                .Where(g => goatIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name })
                .ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        var top = logs
            .GroupBy(l => l.GoatId)
            .Select(g =>
            {
                goatNames.TryGetValue(g.Key, out var name);
                var totalLbs = g.Sum(x => x.Amount);
                var days = g.Select(x => x.Date.Date).Distinct().Count();
                return new GoatMilkRowDto(g.Key, name ?? $"Goat #{g.Key}", totalLbs, days > 0 ? totalLbs / days : 0, days);
            })
            .OrderByDescending(r => r.TotalLbs)
            .Take(20)
            .ToList();

        return new MilkTrendsReportDto(
            new ReportWindowDto(start, end.AddDays(-1)),
            total, dailyAvg, daily, top);
    }

    // ---------- Kidding ----------

    public async Task<KiddingReportDto> GetKiddingAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var (start, end) = Window(from, to);
        var recs = await _db.KiddingRecords
            .AsNoTracking()
            .Where(k => k.KiddingDate >= start && k.KiddingDate < end)
            .Select(k => new { k.Id, k.KiddingDate, k.KidsBorn, k.KidsAlive })
            .ToListAsync(ct);

        int count = recs.Count;
        int born = recs.Sum(r => r.KidsBorn);
        int alive = recs.Sum(r => r.KidsAlive);
        int died = Math.Max(0, born - alive);
        double liveRate = born == 0 ? 0 : (double)alive / born;
        double avgKids = count == 0 ? 0 : (double)born / count;

        int singles = recs.Count(r => r.KidsBorn == 1);
        int twins = recs.Count(r => r.KidsBorn == 2);
        int tripletPlus = recs.Count(r => r.KidsBorn >= 3);

        var monthly = recs
            .GroupBy(r => new { r.KiddingDate.Year, r.KiddingDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyKiddingDto(g.Key.Year, g.Key.Month, g.Count(), g.Sum(x => x.KidsBorn), g.Sum(x => x.KidsAlive)))
            .ToList();

        return new KiddingReportDto(
            new ReportWindowDto(start, end.AddDays(-1)),
            count, born, alive, died, liveRate, avgKids,
            singles, twins, tripletPlus, monthly);
    }

    // ---------- Mortality ----------

    // Uses Goat.UpdatedAt as a proxy for StatusChangedAt. The UI should label
    // this "approximate" since any other field edit would also bump the stamp.
    public async Task<MortalityReportDto> GetMortalityAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var (start, end) = Window(from, to);

        var deceased = await _db.Goats
            .AsNoTracking()
            .Where(g => g.Status == GoatStatus.Deceased
                        && g.UpdatedAt >= start && g.UpdatedAt < end)
            .Select(g => new { g.Id, g.Name, g.UpdatedAt })
            .ToListAsync(ct);

        int activeAtStart = await _db.Goats
            .AsNoTracking()
            .CountAsync(g => g.CreatedAt < start
                             && (g.Status != GoatStatus.Deceased || g.UpdatedAt >= start)
                             && g.Status != GoatStatus.Sold, ct);

        var monthly = deceased
            .GroupBy(d => new { d.UpdatedAt.Year, d.UpdatedAt.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyMortalityDto(g.Key.Year, g.Key.Month, g.Count()))
            .ToList();

        var goats = deceased
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new MortalityGoatDto(d.Id, d.Name, d.UpdatedAt))
            .ToList();

        return new MortalityReportDto(
            new ReportWindowDto(start, end.AddDays(-1)),
            deceased.Count, activeAtStart, monthly, goats);
    }

    // ---------- Parasite / FAMACHA ----------

    public async Task<ParasiteReportDto> GetParasiteAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var (start, end) = Window(from, to);
        var scores = await _db.FamachaScores
            .AsNoTracking()
            .Where(s => s.Date >= start && s.Date < end)
            .Select(s => new { s.GoatId, s.Score, s.Date })
            .ToListAsync(ct);

        int cnt = scores.Count;
        double avg = cnt == 0 ? 0 : scores.Average(s => s.Score);
        int danger = scores.Count(s => s.Score >= 4);
        double dangerPct = cnt == 0 ? 0 : (double)danger / cnt;

        var monthly = scores
            .GroupBy(s => new { s.Date.Year, s.Date.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyFamachaDto(
                g.Key.Year, g.Key.Month,
                g.Average(x => (double)x.Score),
                g.Count(x => x.Score >= 4)))
            .ToList();

        var goatIds = scores.Select(s => s.GoatId).Distinct().ToList();
        var goatNames = goatIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Goats.AsNoTracking()
                .Where(g => goatIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name })
                .ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        var worst = scores
            .GroupBy(s => s.GoatId)
            .Select(g =>
            {
                var latest = g.OrderByDescending(x => x.Date).First();
                goatNames.TryGetValue(g.Key, out var name);
                return new GoatFamachaDto(g.Key, name ?? $"Goat #{g.Key}",
                    g.Average(x => (double)x.Score), latest.Score, latest.Date);
            })
            .OrderByDescending(r => r.AverageScore)
            .Take(20)
            .ToList();

        return new ParasiteReportDto(
            new ReportWindowDto(start, end.AddDays(-1)),
            cnt, avg, danger, dangerPct, monthly, worst);
    }

    // ---------- Health spend ----------

    public async Task<HealthSpendReportDto> GetHealthSpendAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var (start, end) = Window(from, to);
        // "Health spend" = expenses in Veterinary or Supplies categories (Supplies
        // captures first-aid/med purchases). Tweak as reporting needs evolve.
        var healthCats = new[] { "Veterinary", "Supplies" };

        var txns = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.Type == TransactionType.Expense
                        && t.Date >= start && t.Date < end
                        && t.Category != null
                        && healthCats.Contains(t.Category))
            .Select(t => new { t.Amount, t.Category, t.Date, t.GoatId })
            .ToListAsync(ct);

        decimal total = txns.Sum(t => t.Amount);
        int activeGoats = await _db.Goats.AsNoTracking()
            .CountAsync(g => g.Status != GoatStatus.Deceased && g.Status != GoatStatus.Sold, ct);
        decimal avgPerGoat = activeGoats == 0 ? 0 : total / activeGoats;

        var byCat = txns
            .GroupBy(t => t.Category ?? "Uncategorized")
            .Select(g => new CategoryTotalDto(g.Key, g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Total)
            .ToList();

        var goatIds = txns.Where(t => t.GoatId.HasValue).Select(t => t.GoatId!.Value).Distinct().ToList();
        var goatNames = goatIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Goats.AsNoTracking()
                .Where(g => goatIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name })
                .ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        var byGoat = txns.Where(t => t.GoatId.HasValue)
            .GroupBy(t => t.GoatId!.Value)
            .Select(g =>
            {
                goatNames.TryGetValue(g.Key, out var name);
                return new GoatSpendRowDto(g.Key, name ?? $"Goat #{g.Key}", g.Sum(x => x.Amount));
            })
            .OrderByDescending(r => r.Total)
            .ToList();

        var monthly = txns
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyAmountDto(g.Key.Year, g.Key.Month, g.Sum(x => x.Amount)))
            .ToList();

        return new HealthSpendReportDto(
            new ReportWindowDto(start, end.AddDays(-1)),
            total, avgPerGoat, byCat, byGoat, monthly);
    }

    // ---------- Progeny ----------

    // Rolls up offspring performance per sire and per dam. "Offspring" = goats
    // whose DOB falls in [from, to] and who list a given parent. Dam-only
    // metrics (kidding count, live-birth rate, avg litter size) come from
    // KiddingRecord joined through BreedingRecord.DoeId. Daughter milk yield
    // sums MilkLogs for this parent's daughters within the window — provides a
    // quick "who produces productive daughters" signal without requiring the
    // daughters themselves to have been born in the window.
    public async Task<ProgenyReportDto> GetProgenyAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var (start, end) = Window(from, to);

        // Offspring born in window, minimal projection.
        var offspring = await _db.Goats.AsNoTracking()
            .Where(g => g.DateOfBirth != null
                        && g.DateOfBirth >= start && g.DateOfBirth < end)
            .Select(g => new { g.Id, g.SireId, g.DamId, g.Status, g.Gender })
            .ToListAsync(ct);

        // Kids with a linked goat among the offspring — carries birth weight.
        var offspringIds = offspring.Select(o => o.Id).ToList();
        var kidWeights = offspringIds.Count == 0
            ? new List<(int GoatId, double Weight)>()
            : (await _db.Kids.AsNoTracking()
                .Where(k => k.LinkedGoatId != null
                            && offspringIds.Contains(k.LinkedGoatId!.Value)
                            && k.BirthWeightLbs != null)
                .Select(k => new { k.LinkedGoatId, k.BirthWeightLbs })
                .ToListAsync(ct))
                .Select(k => (GoatId: k.LinkedGoatId!.Value, Weight: k.BirthWeightLbs!.Value))
                .ToList();
        var weightByOffspring = kidWeights
            .GroupBy(k => k.GoatId)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Weight));

        // Distinct parent IDs across both roles. Use a tuple so a goat that's
        // both a sire and a dam (impossible biologically, but defensive) lands
        // as two rows — the report is per (ParentId, Gender).
        var sireIds = offspring.Where(o => o.SireId.HasValue).Select(o => o.SireId!.Value).Distinct().ToList();
        var damIds = offspring.Where(o => o.DamId.HasValue).Select(o => o.DamId!.Value).Distinct().ToList();
        var allParentIds = sireIds.Concat(damIds).Distinct().ToList();

        if (allParentIds.Count == 0)
        {
            return new ProgenyReportDto(new ReportWindowDto(start, end.AddDays(-1)), new List<ProgenyRowDto>());
        }

        var parentLookup = await _db.Goats.AsNoTracking()
            .Where(g => allParentIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Name, g.Gender })
            .ToDictionaryAsync(g => g.Id, g => (g.Name, g.Gender), ct);

        // Dam-side kidding stats (scoped by KiddingDate in window).
        var damKiddings = damIds.Count == 0
            ? new Dictionary<int, (int Count, int Born, int Alive)>()
            : (await _db.KiddingRecords.AsNoTracking()
                .Where(k => k.KiddingDate >= start && k.KiddingDate < end
                            && damIds.Contains(k.BreedingRecord.DoeId))
                .Select(k => new { DoeId = k.BreedingRecord.DoeId, k.KidsBorn, k.KidsAlive })
                .ToListAsync(ct))
                .GroupBy(k => k.DoeId)
                .ToDictionary(
                    g => g.Key,
                    g => (Count: g.Count(), Born: g.Sum(x => x.KidsBorn), Alive: g.Sum(x => x.KidsAlive)));

        // Daughter milk: for each parent, find female offspring (NOT restricted
        // to the window — we want established daughters producing now), then
        // aggregate MilkLogs in the window. Pull daughters + their milk in two
        // scoped queries keyed by the parent id set.
        var daughters = await _db.Goats.AsNoTracking()
            .Where(g => g.Gender == Gender.Female
                        && ((g.SireId != null && allParentIds.Contains(g.SireId.Value))
                            || (g.DamId != null && allParentIds.Contains(g.DamId.Value))))
            .Select(g => new { g.Id, g.SireId, g.DamId })
            .ToListAsync(ct);

        var daughterIds = daughters.Select(d => d.Id).ToList();
        var milkByDaughter = daughterIds.Count == 0
            ? new Dictionary<int, (double Total, int Days)>()
            : (await _db.MilkLogs.AsNoTracking()
                .Where(m => m.Date >= start && m.Date < end && daughterIds.Contains(m.GoatId))
                .Select(m => new { m.GoatId, m.Amount, Day = m.Date.Date })
                .ToListAsync(ct))
                .GroupBy(m => m.GoatId)
                .ToDictionary(
                    g => g.Key,
                    g => (Total: g.Sum(x => x.Amount), Days: g.Select(x => x.Day).Distinct().Count()));

        // Lookup: parent → daughter ids (per role).
        var daughtersBySire = daughters.Where(d => d.SireId.HasValue)
            .GroupBy(d => d.SireId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
        var daughtersByDam = daughters.Where(d => d.DamId.HasValue)
            .GroupBy(d => d.DamId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var rows = new List<ProgenyRowDto>();

        foreach (var sireId in sireIds)
        {
            var off = offspring.Where(o => o.SireId == sireId).ToList();
            rows.Add(BuildRow(sireId, Gender.Male, off, weightByOffspring,
                damKidding: null,
                daughterIds: daughtersBySire.GetValueOrDefault(sireId) ?? new List<int>(),
                milkByDaughter, parentLookup));
        }

        foreach (var damId in damIds)
        {
            var off = offspring.Where(o => o.DamId == damId).ToList();
            damKiddings.TryGetValue(damId, out var kidStats);
            rows.Add(BuildRow(damId, Gender.Female, off, weightByOffspring,
                damKidding: damKiddings.ContainsKey(damId) ? kidStats : ((int, int, int)?)null,
                daughterIds: daughtersByDam.GetValueOrDefault(damId) ?? new List<int>(),
                milkByDaughter, parentLookup));
        }

        // Default: most prolific first, ties broken by live-offspring count.
        var ordered = rows
            .OrderByDescending(r => r.OffspringCount)
            .ThenByDescending(r => r.LiveOffspringCount)
            .ToList();

        return new ProgenyReportDto(new ReportWindowDto(start, end.AddDays(-1)), ordered);
    }

    private static ProgenyRowDto BuildRow(
        int parentId,
        Gender parentGender,
        IReadOnlyList<dynamic> off,
        Dictionary<int, double> weightByOffspring,
        (int Count, int Born, int Alive)? damKidding,
        List<int> daughterIds,
        Dictionary<int, (double Total, int Days)> milkByDaughter,
        Dictionary<int, (string Name, Gender Gender)> parentLookup)
    {
        parentLookup.TryGetValue(parentId, out var info);
        var parentName = info.Name ?? $"Goat #{parentId}";

        int count = off.Count;
        int live = off.Count(o => o.Status != GoatStatus.Deceased);

        var weights = off.Select(o => (int)o.Id)
            .Where(id => weightByOffspring.ContainsKey(id))
            .Select(id => weightByOffspring[id])
            .ToList();
        double? avgBirthWeight = weights.Count == 0 ? null : weights.Average();

        int? kidCount = damKidding?.Count;
        int? kidsBorn = damKidding?.Born;
        int? kidsAlive = damKidding?.Alive;
        double? liveBirthRate = damKidding.HasValue && damKidding.Value.Born > 0
            ? (double)damKidding.Value.Alive / damKidding.Value.Born
            : (double?)null;
        double? avgLitter = damKidding.HasValue && damKidding.Value.Count > 0
            ? (double)damKidding.Value.Born / damKidding.Value.Count
            : (double?)null;

        var dailyAverages = daughterIds
            .Where(milkByDaughter.ContainsKey)
            .Select(id => milkByDaughter[id])
            .Where(m => m.Days > 0)
            .Select(m => m.Total / m.Days)
            .ToList();
        double? avgDaughterDailyMilk = dailyAverages.Count == 0 ? null : dailyAverages.Average();
        int daughtersWithMilk = dailyAverages.Count;

        return new ProgenyRowDto(
            parentId, parentName, parentGender,
            count, live,
            kidCount, kidsBorn, kidsAlive,
            liveBirthRate, avgLitter,
            avgBirthWeight,
            avgDaughterDailyMilk, daughtersWithMilk);
    }
}
