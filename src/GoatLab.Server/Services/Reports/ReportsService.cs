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
}
