using GoatLab.Server.Data;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Alerts;

// Materializes per-row Alert entries from domain data. Idempotent: repeated
// scans within a 24h window do NOT re-create alerts the scanner has already
// produced for the same source row. Scanner runs cross-tenant from a Hangfire
// job, so it bypasses query filters and stamps TenantId explicitly.
public class AlertScannerService
{
    private const double WeightDropThreshold = 0.10; // 10%
    private const int UpcomingKiddingDays = 14;
    private const int UpcomingMedicationDays = 7;

    private readonly GoatLabDbContext _db;
    private readonly ILogger<AlertScannerService> _logger;

    public AlertScannerService(GoatLabDbContext db, ILogger<AlertScannerService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Scan a single tenant's data and create new Alert rows for findings that
    /// aren't already represented by an undismissed alert. Returns the alerts
    /// that were freshly created (so the caller can fan out push notifications).
    /// </summary>
    public async Task<List<Alert>> ScanTenantAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var existing = await _db.Alerts
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.DismissedAt == null)
            .Select(a => new { a.Type, a.EntityType, a.EntityId })
            .ToListAsync(cancellationToken);

        var existingKeys = existing
            .Select(e => $"{(int)e.Type}:{e.EntityType}:{e.EntityId}")
            .ToHashSet();

        var fresh = new List<Alert>();

        // 1. Overdue medications (NextDueDate <= now and not yet alerted)
        var overdueMeds = await _db.MedicalRecords
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId
                     && r.NextDueDate != null
                     && r.NextDueDate <= now)
            .Include(r => r.Goat)
            .ToListAsync(cancellationToken);
        foreach (var m in overdueMeds)
        {
            var key = $"{(int)AlertType.MedicationOverdue}:MedicalRecord:{m.Id}";
            if (existingKeys.Contains(key)) continue;
            fresh.Add(new Alert
            {
                TenantId = tenantId,
                Type = AlertType.MedicationOverdue,
                Severity = AlertSeverity.Error,
                Title = $"Overdue: {m.Title}",
                Body = $"{m.Goat?.Name ?? "Unknown"} — was due {m.NextDueDate:MMM d}",
                EntityType = "MedicalRecord",
                EntityId = m.Id,
                DeepLink = $"/herd/{m.GoatId}",
            });
        }

        // 2. Upcoming medications (NextDueDate within next 7 days)
        var upcomingMeds = await _db.MedicalRecords
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId
                     && r.NextDueDate != null
                     && r.NextDueDate > now
                     && r.NextDueDate <= now.AddDays(UpcomingMedicationDays))
            .Include(r => r.Goat)
            .ToListAsync(cancellationToken);
        foreach (var m in upcomingMeds)
        {
            var key = $"{(int)AlertType.MedicationDue}:MedicalRecord:{m.Id}";
            if (existingKeys.Contains(key)) continue;
            fresh.Add(new Alert
            {
                TenantId = tenantId,
                Type = AlertType.MedicationDue,
                Severity = AlertSeverity.Warning,
                Title = $"Due soon: {m.Title}",
                Body = $"{m.Goat?.Name ?? "Unknown"} — due {m.NextDueDate:MMM d}",
                EntityType = "MedicalRecord",
                EntityId = m.Id,
                DeepLink = $"/herd/{m.GoatId}",
            });
        }

        // 3. Overdue kiddings (estimated date passed, outcome still Pending and no kidding recorded)
        var overdueKiddings = await _db.BreedingRecords
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId
                     && b.Outcome == BreedingOutcome.Pending
                     && b.EstimatedDueDate != null
                     && b.EstimatedDueDate < now
                     && !b.KiddingRecords.Any())
            .Include(b => b.Doe)
            .ToListAsync(cancellationToken);
        foreach (var b in overdueKiddings)
        {
            var key = $"{(int)AlertType.KiddingOverdue}:BreedingRecord:{b.Id}";
            if (existingKeys.Contains(key)) continue;
            fresh.Add(new Alert
            {
                TenantId = tenantId,
                Type = AlertType.KiddingOverdue,
                Severity = AlertSeverity.Error,
                Title = $"Kidding overdue: {b.Doe?.Name ?? "Doe"}",
                Body = $"Estimated due {b.EstimatedDueDate:MMM d} — record kidding or update outcome.",
                EntityType = "BreedingRecord",
                EntityId = b.Id,
                DeepLink = "/breeding",
            });
        }

        // 4. Upcoming kiddings within 14 days
        var upcomingKiddings = await _db.BreedingRecords
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId
                     && b.Outcome == BreedingOutcome.Confirmed
                     && b.EstimatedDueDate != null
                     && b.EstimatedDueDate > now
                     && b.EstimatedDueDate <= now.AddDays(UpcomingKiddingDays))
            .Include(b => b.Doe)
            .ToListAsync(cancellationToken);
        foreach (var b in upcomingKiddings)
        {
            var key = $"{(int)AlertType.KiddingUpcoming}:BreedingRecord:{b.Id}";
            if (existingKeys.Contains(key)) continue;
            var days = Math.Max(1, (int)Math.Ceiling((b.EstimatedDueDate!.Value - now).TotalDays));
            fresh.Add(new Alert
            {
                TenantId = tenantId,
                Type = AlertType.KiddingUpcoming,
                Severity = AlertSeverity.Info,
                Title = $"Kidding in {days} day{(days == 1 ? "" : "s")}: {b.Doe?.Name ?? "Doe"}",
                Body = $"Due {b.EstimatedDueDate:MMM d}. Get the kidding kit ready.",
                EntityType = "BreedingRecord",
                EntityId = b.Id,
                DeepLink = "/breeding",
            });
        }

        // 5. Low feed stock (configured threshold reached)
        var lowFeed = await _db.FeedInventory
            .IgnoreQueryFilters()
            .Where(f => f.TenantId == tenantId
                     && f.LowStockThreshold != null
                     && f.QuantityOnHand <= f.LowStockThreshold)
            .ToListAsync(cancellationToken);
        foreach (var f in lowFeed)
        {
            var key = $"{(int)AlertType.LowFeedStock}:FeedInventory:{f.Id}";
            if (existingKeys.Contains(key)) continue;
            fresh.Add(new Alert
            {
                TenantId = tenantId,
                Type = AlertType.LowFeedStock,
                Severity = AlertSeverity.Warning,
                Title = $"Low feed: {f.FeedName}",
                Body = $"{f.QuantityOnHand:0.##} {f.Unit ?? "units"} remaining (threshold {f.LowStockThreshold:0.##}).",
                EntityType = "FeedInventory",
                EntityId = f.Id,
                DeepLink = "/inventory",
            });
        }

        // 6. Weight drops — compare each goat's last two weight records
        var weightsByGoat = await _db.WeightRecords
            .IgnoreQueryFilters()
            .Where(w => w.TenantId == tenantId)
            .OrderByDescending(w => w.Date)
            .GroupBy(w => w.GoatId)
            .Select(g => new
            {
                GoatId = g.Key,
                Latest = g.OrderByDescending(w => w.Date).Take(2).ToList(),
            })
            .ToListAsync(cancellationToken);
        var goatNames = await _db.Goats
            .IgnoreQueryFilters()
            .Where(g => g.TenantId == tenantId)
            .ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);
        foreach (var w in weightsByGoat)
        {
            if (w.Latest.Count < 2) continue;
            var latest = w.Latest[0];
            var prev = w.Latest[1];
            if (prev.Weight <= 0) continue;
            var drop = (prev.Weight - latest.Weight) / prev.Weight;
            if (drop < WeightDropThreshold) continue;

            var key = $"{(int)AlertType.WeightDrop}:WeightRecord:{latest.Id}";
            if (existingKeys.Contains(key)) continue;

            var pct = (int)Math.Round(drop * 100);
            goatNames.TryGetValue(w.GoatId, out var name);
            fresh.Add(new Alert
            {
                TenantId = tenantId,
                Type = AlertType.WeightDrop,
                Severity = AlertSeverity.Warning,
                Title = $"Weight drop: {name ?? "Goat"} ({pct}%)",
                Body = $"From {prev.Weight:0.#} lb on {prev.Date:MMM d} to {latest.Weight:0.#} lb on {latest.Date:MMM d}.",
                EntityType = "WeightRecord",
                EntityId = latest.Id,
                DeepLink = $"/herd/{w.GoatId}",
            });
        }

        if (fresh.Count == 0)
        {
            _logger.LogInformation("Alert scan: tenant {TenantId} — no new alerts", tenantId);
            return fresh;
        }

        _db.Alerts.AddRange(fresh);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Alert scan: tenant {TenantId} — {Count} new alerts", tenantId, fresh.Count);
        return fresh;
    }
}
