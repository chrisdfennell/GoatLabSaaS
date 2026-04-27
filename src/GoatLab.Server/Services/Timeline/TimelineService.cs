using GoatLab.Server.Data;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Timeline;

// Builds a chronological event log for a single goat. Aggregates from every
// per-goat data source the app collects (12+ sources today) so the user gets
// one scrollable history instead of bouncing between 8 tabs.
//
// Tenant isolation comes from the existing global query filter; the controller
// just verifies the goat belongs to the current tenant before calling in.
//
// Performance note: each source is capped (Take(N)) so a goat with thousands
// of milk logs doesn't blow up the timeline. Milk in particular is grouped by
// month server-side to keep dairy-doe timelines readable. If we ever need
// "every milking" granularity, the existing /api/milk endpoint already serves
// that — the timeline is the wide-angle lens.
public interface ITimelineService
{
    Task<List<TimelineEntryDto>> GetForGoatAsync(int goatId, CancellationToken ct = default);
}

public class TimelineService : ITimelineService
{
    private readonly GoatLabDbContext _db;
    public TimelineService(GoatLabDbContext db) => _db = db;

    private const int PerSourceCap = 200;
    private const string SevInfo = "info";
    private const string SevSuccess = "success";
    private const string SevWarning = "warning";
    private const string SevError = "error";

    public async Task<List<TimelineEntryDto>> GetForGoatAsync(int goatId, CancellationToken ct = default)
    {
        var entries = new List<TimelineEntryDto>(512);

        var goat = await _db.Goats
            .AsNoTracking()
            .Where(g => g.Id == goatId)
            .Select(g => new
            {
                g.Id, g.Name, g.Status, g.CreatedAt, g.StatusChangedAt
            })
            .FirstOrDefaultAsync(ct);
        if (goat is null) return entries;

        // Lifecycle bookends (always present).
        entries.Add(new TimelineEntryDto(
            goat.CreatedAt, "added", "Added to herd",
            $"{goat.Name} was added to the herd.",
            $"/herd/{goat.Id}", SevSuccess, "pets"));

        if (goat.StatusChangedAt is DateTime statusAt
            && goat.Status != GoatStatus.Healthy
            && (statusAt - goat.CreatedAt).TotalMinutes > 1) // ignore the create-time stamp
        {
            entries.Add(new TimelineEntryDto(
                statusAt, "status",
                $"Status changed to {goat.Status}",
                null,
                $"/herd/{goat.Id}",
                goat.Status switch
                {
                    GoatStatus.Deceased => SevError,
                    GoatStatus.Sick or GoatStatus.AtVet => SevWarning,
                    _ => SevInfo
                },
                "swap_horiz"));
        }

        // ---- Medical records ----
        var meds = await _db.MedicalRecords
            .AsNoTracking()
            .Where(m => m.GoatId == goatId)
            .OrderByDescending(m => m.Date).Take(PerSourceCap)
            .Select(m => new { m.Id, m.Date, m.RecordType, m.Title, m.Description, m.Dosage, m.MilkWithdrawalEndsAt, m.MeatWithdrawalEndsAt })
            .ToListAsync(ct);
        foreach (var m in meds)
        {
            var hasWithdrawal = (m.MilkWithdrawalEndsAt ?? m.MeatWithdrawalEndsAt) is not null;
            var detail = string.Join(" · ", new[]
            {
                m.RecordType.ToString(),
                m.Description,
                m.Dosage,
                hasWithdrawal ? "withdrawal hold active" : null
            }.Where(s => !string.IsNullOrWhiteSpace(s)));
            entries.Add(new TimelineEntryDto(
                m.Date, "medical", m.Title,
                string.IsNullOrEmpty(detail) ? null : detail,
                $"/health?goatId={goatId}",
                hasWithdrawal ? SevWarning : SevInfo,
                "local_hospital"));
        }

        // ---- Weights ----
        var weights = await _db.WeightRecords
            .AsNoTracking()
            .Where(w => w.GoatId == goatId)
            .OrderByDescending(w => w.Date).Take(PerSourceCap)
            .Select(w => new { w.Date, w.Weight })
            .ToListAsync(ct);
        // Flag drops ≥10% from the previous record as warnings.
        var weightAsc = weights.OrderBy(w => w.Date).ToList();
        for (var i = 0; i < weightAsc.Count; i++)
        {
            var w = weightAsc[i];
            var sev = SevInfo;
            string? detail = null;
            if (i > 0)
            {
                var prev = weightAsc[i - 1];
                var diff = w.Weight - prev.Weight;
                var pct = prev.Weight > 0 ? diff / prev.Weight : 0;
                if (pct <= -0.10)
                {
                    sev = SevWarning;
                    detail = $"Down {Math.Abs(diff):F1} lbs ({pct * 100:F0}%) from previous";
                }
                else if (Math.Abs(diff) >= 1)
                {
                    detail = diff > 0 ? $"+{diff:F1} lbs" : $"{diff:F1} lbs";
                }
            }
            entries.Add(new TimelineEntryDto(
                w.Date, "weight", $"Weighed {w.Weight:F1} lbs", detail,
                $"/health?goatId={goatId}", sev, "scale"));
        }

        // ---- FAMACHA ----
        var famacha = await _db.FamachaScores
            .AsNoTracking()
            .Where(f => f.GoatId == goatId)
            .OrderByDescending(f => f.Date).Take(PerSourceCap)
            .Select(f => new { f.Date, f.Score })
            .ToListAsync(ct);
        foreach (var f in famacha)
        {
            entries.Add(new TimelineEntryDto(
                f.Date, "famacha", $"FAMACHA {f.Score}/5",
                f.Score >= 4 ? "Danger zone — consider deworming" : null,
                $"/health?goatId={goatId}",
                f.Score >= 4 ? SevError : f.Score == 3 ? SevWarning : SevInfo,
                "favorite"));
        }

        // ---- Body condition ----
        var bcs = await _db.BodyConditionScores
            .AsNoTracking()
            .Where(b => b.GoatId == goatId)
            .OrderByDescending(b => b.Date).Take(PerSourceCap)
            .Select(b => new { b.Date, b.Score })
            .ToListAsync(ct);
        foreach (var b in bcs)
        {
            entries.Add(new TimelineEntryDto(
                b.Date, "bcs", $"Body condition {b.Score:F1}/5", null,
                $"/health?goatId={goatId}", SevInfo, "fitness_center"));
        }

        // ---- Milk: monthly rollup so a dairy doe doesn't show 365 entries/yr ----
        var milkRows = await _db.MilkLogs
            .AsNoTracking()
            .Where(m => m.GoatId == goatId)
            .Select(m => new { m.Date, m.Amount })
            .ToListAsync(ct);
        var milkByMonth = milkRows
            .GroupBy(m => new { m.Date.Year, m.Date.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Total = g.Sum(x => x.Amount),
                Sessions = g.Count(),
                LastDate = g.Max(x => x.Date)
            })
            .OrderByDescending(g => g.LastDate)
            .Take(36) // 3 years of monthly milk roll-ups is plenty
            .ToList();
        foreach (var mm in milkByMonth)
        {
            var monthLabel = new DateTime(mm.Year, mm.Month, 1).ToString("MMM yyyy");
            entries.Add(new TimelineEntryDto(
                mm.LastDate, "milk",
                $"{mm.Total:F1} lbs milked in {monthLabel}",
                $"{mm.Sessions} {(mm.Sessions == 1 ? "session" : "sessions")} · avg {mm.Total / mm.Sessions:F1} lbs/session",
                $"/production?goatId={goatId}", SevInfo, "water_drop"));
        }

        // ---- Breeding records (the doe is the focal goat) ----
        var breedings = await _db.BreedingRecords
            .AsNoTracking()
            .Include(b => b.Buck)
            .Where(b => b.DoeId == goatId)
            .OrderByDescending(b => b.BreedingDate).Take(PerSourceCap)
            .Select(b => new { b.Id, b.BreedingDate, b.EstimatedDueDate, b.Outcome, BuckName = b.Buck != null ? b.Buck.Name : null })
            .ToListAsync(ct);
        foreach (var br in breedings)
        {
            var sireBit = string.IsNullOrEmpty(br.BuckName) ? "" : $"to {br.BuckName}";
            entries.Add(new TimelineEntryDto(
                br.BreedingDate, "breeding", $"Bred {sireBit}".Trim(),
                br.EstimatedDueDate.HasValue ? $"Due {br.EstimatedDueDate:MMM d, yyyy}" : null,
                "/breeding", SevInfo, "favorite"));
        }

        // ---- Kidding records ----
        var kiddings = await _db.KiddingRecords
            .AsNoTracking()
            .Where(k => k.BreedingRecord != null && k.BreedingRecord.DoeId == goatId)
            .OrderByDescending(k => k.KiddingDate).Take(PerSourceCap)
            .Select(k => new { k.KiddingDate, k.KidsBorn, k.KidsAlive, k.Outcome, k.AssistanceGiven })
            .ToListAsync(ct);
        foreach (var k in kiddings)
        {
            var detailParts = new List<string>();
            if (k.KidsAlive < k.KidsBorn) detailParts.Add($"{k.KidsBorn - k.KidsAlive} stillborn");
            if (k.AssistanceGiven != AssistanceLevel.None) detailParts.Add($"{k.AssistanceGiven} assistance");
            if (k.Outcome != KiddingOutcome.Healthy) detailParts.Add(k.Outcome.ToString());
            entries.Add(new TimelineEntryDto(
                k.KiddingDate, "kidding",
                $"Kidded — {k.KidsAlive} of {k.KidsBorn} {(k.KidsBorn == 1 ? "kid" : "kids")} alive",
                detailParts.Count > 0 ? string.Join(" · ", detailParts) : null,
                "/breeding",
                k.KidsAlive == 0 ? SevError : k.KidsAlive < k.KidsBorn ? SevWarning : SevSuccess,
                "child_care"));
        }

        // ---- Shows ----
        var shows = await _db.ShowRecords
            .AsNoTracking()
            .Where(s => s.GoatId == goatId)
            .OrderByDescending(s => s.ShowDate).Take(PerSourceCap)
            .Select(s => new { s.ShowDate, s.ShowName, s.Class, s.Placing, s.ClassSize, s.Awards })
            .ToListAsync(ct);
        foreach (var s in shows)
        {
            var detail = s.Placing.HasValue
                ? $"Placed {Ordinal(s.Placing.Value)}{(s.ClassSize.HasValue ? $" of {s.ClassSize}" : "")}{(string.IsNullOrEmpty(s.Class) ? "" : $" in {s.Class}")}{(string.IsNullOrEmpty(s.Awards) ? "" : $" · {s.Awards}")}"
                : s.Class;
            var sev = (s.Placing == 1) ? SevSuccess : SevInfo;
            entries.Add(new TimelineEntryDto(
                s.ShowDate, "show", s.ShowName, detail,
                $"/herd/{goatId}", sev, "emoji_events"));
        }

        // ---- Linear appraisal ----
        var appraisals = await _db.LinearAppraisals
            .AsNoTracking()
            .Where(a => a.GoatId == goatId)
            .OrderByDescending(a => a.AppraisalDate).Take(PerSourceCap)
            .Select(a => new { a.AppraisalDate, a.Appraiser, a.FinalScore, a.Classification })
            .ToListAsync(ct);
        foreach (var a in appraisals)
        {
            var detail = a.FinalScore.HasValue ? $"Final score {a.FinalScore}{(a.Classification.HasValue ? $" · {a.Classification}" : "")}" : null;
            entries.Add(new TimelineEntryDto(
                a.AppraisalDate, "appraisal",
                $"Linear appraisal{(string.IsNullOrEmpty(a.Appraiser) ? "" : $" by {a.Appraiser}")}",
                detail, $"/herd/{goatId}", SevInfo, "rule"));
        }

        // ---- Harvest ----
        var harvest = await _db.HarvestRecords
            .AsNoTracking()
            .Where(h => h.GoatId == goatId)
            .Select(h => new { h.HarvestDate, h.Processor, h.HangingWeight })
            .ToListAsync(ct);
        foreach (var h in harvest)
        {
            entries.Add(new TimelineEntryDto(
                h.HarvestDate, "harvest", "Harvested",
                $"{(string.IsNullOrEmpty(h.Processor) ? "" : $"{h.Processor} · ")}{(h.HangingWeight.HasValue ? $"{h.HangingWeight} lbs hanging" : "")}".Trim(' ', '·'),
                $"/herd/{goatId}", SevError, "inventory_2"));
        }

        // ---- Photos & documents ----
        var photos = await _db.GoatPhotos
            .AsNoTracking()
            .Where(p => p.GoatId == goatId)
            .OrderByDescending(p => p.UploadedAt).Take(PerSourceCap)
            .Select(p => new { p.UploadedAt, p.Caption, p.IsPrimary })
            .ToListAsync(ct);
        foreach (var p in photos)
        {
            entries.Add(new TimelineEntryDto(
                p.UploadedAt, "photo",
                p.IsPrimary ? "Primary photo set" : "Photo added",
                p.Caption,
                $"/herd/{goatId}", SevInfo, "photo_camera"));
        }
        var docs = await _db.GoatDocuments
            .AsNoTracking()
            .Where(d => d.GoatId == goatId)
            .OrderByDescending(d => d.UploadedAt).Take(PerSourceCap)
            .Select(d => new { d.UploadedAt, d.Title, d.DocumentType })
            .ToListAsync(ct);
        foreach (var d in docs)
        {
            entries.Add(new TimelineEntryDto(
                d.UploadedAt, "document",
                $"Document added: {d.Title}",
                d.DocumentType,
                $"/herd/{goatId}", SevInfo, "folder"));
        }

        // Newest first; secondary sort by Kind so same-day events are stable
        // even when timestamps tied to dates only (not full timestamps).
        return entries
            .OrderByDescending(e => e.Date)
            .ThenBy(e => e.Kind)
            .ToList();
    }

    private static string Ordinal(int n) => (n % 100) switch
    {
        11 or 12 or 13 => $"{n}th",
        _ => (n % 10) switch
        {
            1 => $"{n}st",
            2 => $"{n}nd",
            3 => $"{n}rd",
            _ => $"{n}th"
        }
    };
}
