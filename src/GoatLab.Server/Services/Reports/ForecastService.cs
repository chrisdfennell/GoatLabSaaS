using GoatLab.Server.Data;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Reports;

// Simple forecasting: trailing-average extrapolation. Not ML, not seasonal.
// Good enough for horizons of 30–90 days; users treat these as "if current
// patterns continue" projections, not predictions. All methods honor the
// tenant query filter on the DbContext.
public class ForecastService
{
    private readonly GoatLabDbContext _db;
    public ForecastService(GoatLabDbContext db) => _db = db;

    private static DateTime Today() => DateTime.UtcNow.Date;

    // ---------- Kidding forecast ----------

    public async Task<KiddingForecastDto> GetKiddingForecastAsync(int days = 90, CancellationToken ct = default)
    {
        var today = Today();
        var horizon = today.AddDays(days);

        var upcoming = await _db.BreedingRecords
            .AsNoTracking()
            .Where(b => b.EstimatedDueDate != null
                        && b.EstimatedDueDate >= today
                        && b.EstimatedDueDate <= horizon
                        && b.Outcome != BreedingOutcome.Failed
                        && b.Outcome != BreedingOutcome.Aborted)
            .Where(b => !b.KiddingRecords.Any())
            .Select(b => new { b.Id, b.DoeId, DoeName = b.Doe.Name, Due = b.EstimatedDueDate!.Value })
            .OrderBy(b => b.Due)
            .ToListAsync(ct);

        int count30 = upcoming.Count(u => u.Due <= today.AddDays(30));
        int count60 = upcoming.Count(u => u.Due <= today.AddDays(60));
        int count90 = upcoming.Count(u => u.Due <= today.AddDays(90));

        var items = upcoming.Select(u => new KiddingForecastItemDto(
            u.Id, u.DoeId, u.DoeName, u.Due, (int)(u.Due - today).TotalDays)).ToList();

        return new KiddingForecastDto(today, days, count30, count60, count90, items);
    }

    // ---------- Milk forecast ----------

    // Approach: average the herd's daily totals over the trailing 14 days, then
    // project that average flat across the horizon. Simple and honest — doesn't
    // pretend to model lactation curves. If the farm has zero recent data, we
    // return zeros and let the UI say so.
    public async Task<MilkForecastDto> GetMilkForecastAsync(int days = 60, CancellationToken ct = default)
    {
        var today = Today();
        const int trailingDays = 14;
        var windowStart = today.AddDays(-trailingDays);

        var logs = await _db.MilkLogs
            .AsNoTracking()
            .Where(m => m.Date >= windowStart && m.Date < today.AddDays(1))
            .Select(m => new { m.Date, m.Amount })
            .ToListAsync(ct);

        double total = logs.Sum(l => l.Amount);
        double avg = total / trailingDays;

        var historical = logs
            .GroupBy(l => l.Date.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyTotalDto(g.Key, g.Sum(x => x.Amount)))
            .ToList();

        var projected = Enumerable.Range(1, days)
            .Select(i => new DailyTotalDto(today.AddDays(i), avg))
            .ToList();

        return new MilkForecastDto(today, days, avg, avg * days, historical, projected);
    }

    // ---------- Feed reorder forecast ----------

    // Approach: for each feed item, average daily consumption over the trailing
    // window, project that flat forward, compare against current on-hand. Flags
    // items whose projected remaining hits the low-stock threshold (or zero) in
    // the horizon. Items with zero recent consumption are returned but not
    // flagged — we don't guess reorder dates without signal.
    public async Task<FeedForecastDto> GetFeedForecastAsync(int days = 60, CancellationToken ct = default)
    {
        var today = Today();
        const int trailingDays = 30;
        var windowStart = today.AddDays(-trailingDays);

        var feeds = await _db.FeedInventory
            .AsNoTracking()
            .OrderBy(f => f.FeedName)
            .ToListAsync(ct);

        var usage = await _db.FeedConsumptions
            .AsNoTracking()
            .Where(c => c.Date >= windowStart && c.Date < today.AddDays(1))
            .GroupBy(c => c.FeedInventoryId)
            .Select(g => new { FeedId = g.Key, Total = g.Sum(x => x.Quantity) })
            .ToListAsync(ct);
        var usageByFeed = usage.ToDictionary(u => u.FeedId, u => u.Total);

        var items = feeds.Select(f =>
        {
            var total = usageByFeed.GetValueOrDefault(f.Id, 0.0);
            var daily = total / trailingDays;
            var projectedUse = daily * days;
            var remaining = f.QuantityOnHand - projectedUse;
            int? daysUntilEmpty = daily > 0 && f.QuantityOnHand > 0
                ? (int)Math.Floor(f.QuantityOnHand / daily)
                : (int?)null;
            int? daysUntilLow = daily > 0 && f.LowStockThreshold is double low && f.QuantityOnHand > low
                ? (int)Math.Floor((f.QuantityOnHand - low) / daily)
                : (int?)null;
            bool reorderSoon = daily > 0 &&
                (f.QuantityOnHand <= (f.LowStockThreshold ?? 0)
                 || (daysUntilLow.HasValue && daysUntilLow.Value <= days)
                 || (daysUntilEmpty.HasValue && daysUntilEmpty.Value <= days));

            return new FeedForecastItemDto(
                f.Id, f.FeedName, f.Unit,
                f.QuantityOnHand, f.LowStockThreshold,
                Math.Round(daily, 2), Math.Round(projectedUse, 1), Math.Round(remaining, 1),
                daysUntilEmpty, daysUntilLow, reorderSoon);
        }).ToList();

        return new FeedForecastDto(today, days, trailingDays, items);
    }

    // ---------- Cash-flow forecast ----------

    public async Task<CashflowForecastDto> GetCashflowForecastAsync(int days = 90, CancellationToken ct = default)
    {
        var today = Today();
        const int trailingDays = 90;
        var windowStart = today.AddDays(-trailingDays);

        var txns = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.Date >= windowStart && t.Date < today.AddDays(1))
            .Select(t => new { t.Type, t.Amount })
            .ToListAsync(ct);

        decimal incomeTotal = txns.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        decimal expenseTotal = txns.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
        decimal incomeDaily = incomeTotal / trailingDays;
        decimal expenseDaily = expenseTotal / trailingDays;

        var projected = new List<CashflowDayDto>(days);
        decimal cumulative = 0m;
        for (int i = 1; i <= days; i++)
        {
            cumulative += incomeDaily - expenseDaily;
            projected.Add(new CashflowDayDto(today.AddDays(i), incomeDaily, expenseDaily, cumulative));
        }

        return new CashflowForecastDto(
            today, days,
            incomeDaily, expenseDaily,
            incomeDaily * days, expenseDaily * days, (incomeDaily - expenseDaily) * days,
            projected);
    }
}
