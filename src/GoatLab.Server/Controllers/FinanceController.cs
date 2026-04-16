using GoatLab.Server.Data;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FinanceController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public FinanceController(GoatLabDbContext db) => _db = db;

    // --- Transactions ---

    [HttpGet]
    public async Task<ActionResult<List<Transaction>>> GetAll(
        [FromQuery] TransactionType? type,
        [FromQuery] string? category,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? goatId)
    {
        var query = _db.Transactions.Include(t => t.Goat).AsQueryable();

        if (type.HasValue) query = query.Where(t => t.Type == type.Value);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(t => t.Category == category);
        if (from.HasValue) query = query.Where(t => t.Date >= from.Value);
        if (to.HasValue) query = query.Where(t => t.Date <= to.Value);
        if (goatId.HasValue) query = query.Where(t => t.GoatId == goatId.Value);

        return await query.OrderByDescending(t => t.Date).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Transaction>> Get(int id)
    {
        var txn = await _db.Transactions.Include(t => t.Goat).FirstOrDefaultAsync(t => t.Id == id);
        return txn is null ? NotFound() : txn;
    }

    [HttpPost]
    public async Task<ActionResult<Transaction>> Create(Transaction txn)
    {
        txn.CreatedAt = DateTime.UtcNow;
        _db.Transactions.Add(txn);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = txn.Id }, txn);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Transaction txn)
    {
        if (id != txn.Id) return BadRequest();
        var existing = await _db.Transactions.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Type = txn.Type;
        existing.Date = txn.Date;
        existing.Description = txn.Description;
        existing.Amount = txn.Amount;
        existing.Category = txn.Category;
        existing.GoatId = txn.GoatId;
        existing.Notes = txn.Notes;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var txn = await _db.Transactions.FindAsync(id);
        if (txn is null) return NotFound();
        _db.Transactions.Remove(txn);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Dashboard ---

    [HttpGet("dashboard")]
    public async Task<ActionResult<object>> GetDashboard([FromQuery] int months = 12)
    {
        var from = DateTime.UtcNow.AddMonths(-months);

        var monthly = await _db.Transactions
            .Where(t => t.Date >= from)
            .GroupBy(t => new { t.Date.Year, t.Date.Month, t.Type })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Type,
                Total = g.Sum(t => t.Amount)
            })
            .OrderBy(g => g.Year).ThenBy(g => g.Month)
            .ToListAsync();

        var totalIncome = await _db.Transactions
            .Where(t => t.Type == TransactionType.Income && t.Date >= from)
            .SumAsync(t => t.Amount);

        var totalExpenses = await _db.Transactions
            .Where(t => t.Type == TransactionType.Expense && t.Date >= from)
            .SumAsync(t => t.Amount);

        return Ok(new { monthly, totalIncome, totalExpenses, netProfit = totalIncome - totalExpenses });
    }

    // --- Cost Per Goat ---

    [HttpGet("cost-per-goat")]
    public async Task<ActionResult<object>> GetCostPerGoat()
    {
        var costs = await _db.Transactions
            .Where(t => t.Type == TransactionType.Expense && t.GoatId != null)
            .GroupBy(t => new { t.GoatId, t.Goat!.Name })
            .Select(g => new { GoatId = g.Key.GoatId, GoatName = g.Key.Name, TotalCost = g.Sum(t => t.Amount) })
            .OrderByDescending(g => g.TotalCost)
            .ToListAsync();

        return Ok(costs);
    }

    // --- Per-goat P&L ---

    [HttpGet("goat/{goatId}/pnl")]
    public async Task<ActionResult<object>> GetGoatPnl(int goatId)
    {
        var goat = await _db.Goats.FindAsync(goatId);
        if (goat is null) return NotFound();

        var txns = await _db.Transactions
            .Where(t => t.GoatId == goatId)
            .OrderByDescending(t => t.Date)
            .ToListAsync();

        var income = txns.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        var expenses = txns.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

        var expensesByCategory = txns
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => t.Category ?? "Uncategorized")
            .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
            .OrderByDescending(x => x.Total)
            .ToList();

        // Weight-based cost-per-lb-of-gain
        var weights = await _db.WeightRecords
            .Where(w => w.GoatId == goatId)
            .OrderBy(w => w.Date)
            .ToListAsync();
        double? gain = null;
        decimal? costPerLbGain = null;
        if (weights.Count >= 2)
        {
            gain = weights.Last().Weight - weights.First().Weight;
            if (gain > 0 && expenses > 0)
                costPerLbGain = expenses / (decimal)gain;
        }

        return Ok(new
        {
            goatId,
            goatName = goat.Name,
            income,
            expenses,
            net = income - expenses,
            transactions = txns,
            expensesByCategory,
            weightGain = gain,
            costPerLbGain
        });
    }

    // --- Category breakdown for the dashboard ---

    [HttpGet("expense-breakdown")]
    public async Task<ActionResult<object>> GetExpenseBreakdown([FromQuery] int months = 12)
    {
        var from = DateTime.UtcNow.AddMonths(-months);
        var breakdown = await _db.Transactions
            .Where(t => t.Type == TransactionType.Expense && t.Date >= from)
            .GroupBy(t => t.Category ?? "Uncategorized")
            .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
            .OrderByDescending(x => x.Total)
            .ToListAsync();
        return Ok(breakdown);
    }

    // --- Schedule F tax export (CSV) ---

    [HttpGet("tax-export")]
    public async Task<IActionResult> TaxExport([FromQuery] int year)
    {
        var txns = await _db.Transactions
            .Where(t => t.Date.Year == year)
            .OrderBy(t => t.Date)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Schedule,Line,Date,Description,Category,Amount");

        foreach (var t in txns)
        {
            var (schedule, line) = ScheduleFLine(t);
            var safe = (t.Description ?? "").Replace("\"", "\"\"");
            sb.AppendLine($"{schedule},{line},{t.Date:yyyy-MM-dd},\"{safe}\",{t.Category},{t.Amount}");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"goatlab-tax-{year}.csv");
    }

    private static (string Schedule, string Line) ScheduleFLine(Transaction t)
    {
        if (t.Type == TransactionType.Income)
        {
            return t.Category switch
            {
                "AnimalSale" => ("F", "Line 1a - Sales of livestock"),
                "MilkSale"   => ("F", "Line 3a - Cooperative distributions / raw product"),
                "MeatSale"   => ("F", "Line 2 - Sales of livestock raised"),
                "BreedingFee"=> ("F", "Line 8 - Other income"),
                _            => ("F", "Line 8 - Other income")
            };
        }
        return t.Category switch
        {
            "Feed"        => ("F", "Line 18 - Feed"),
            "Veterinary"  => ("F", "Line 32 - Vet, breeding, medicine"),
            "Equipment"   => ("F", "Line 14 - Depreciation / equipment"),
            "Supplies"    => ("F", "Line 30 - Supplies"),
            "Labor"       => ("F", "Line 22 - Labor hired"),
            "Facility"    => ("F", "Line 26 - Rent or lease"),
            "Transport"   => ("F", "Line 10 - Car and truck"),
            "Insurance"   => ("F", "Line 19 - Insurance (not health)"),
            _             => ("F", "Line 32 - Other expenses")
        };
    }

    // --- Harvest Records ---

    [HttpGet("harvests")]
    public async Task<ActionResult<List<HarvestRecord>>> GetHarvests()
    {
        return await _db.HarvestRecords
            .Include(h => h.Goat)
            .OrderByDescending(h => h.HarvestDate)
            .ToListAsync();
    }

    [HttpPost("harvests")]
    public async Task<ActionResult<HarvestRecord>> CreateHarvest(HarvestRecord record)
    {
        record.CreatedAt = DateTime.UtcNow;
        _db.HarvestRecords.Add(record);
        await _db.SaveChangesAsync();
        return Ok(record);
    }

    [HttpPut("harvests/{id}")]
    public async Task<IActionResult> UpdateHarvest(int id, HarvestRecord record)
    {
        if (id != record.Id) return BadRequest();
        var existing = await _db.HarvestRecords.FindAsync(id);
        if (existing is null) return NotFound();

        existing.GoatId = record.GoatId;
        existing.HarvestDate = record.HarvestDate;
        existing.Processor = record.Processor;
        existing.HangingWeight = record.HangingWeight;
        existing.PackagedWeight = record.PackagedWeight;
        existing.ProcessingCost = record.ProcessingCost;
        existing.LockerLocation = record.LockerLocation;
        existing.Notes = record.Notes;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("harvests/{id}")]
    public async Task<IActionResult> DeleteHarvest(int id)
    {
        var record = await _db.HarvestRecords.FindAsync(id);
        if (record is null) return NotFound();
        _db.HarvestRecords.Remove(record);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
