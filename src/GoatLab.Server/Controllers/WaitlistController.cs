using GoatLab.Server.Data;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequiresFeature(AppFeature.BuyerWaitlist)]
public class WaitlistController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public WaitlistController(GoatLabDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<WaitlistEntry>>> GetAll([FromQuery] WaitlistStatus? status)
    {
        var q = _db.WaitlistEntries
            .Include(w => w.Customer)
            .Include(w => w.FulfilledGoat)
            .Include(w => w.FulfilledSale)
            .AsQueryable();
        if (status.HasValue) q = q.Where(w => w.Status == status.Value);
        return await q.OrderByDescending(w => w.Priority).ThenBy(w => w.CreatedAt).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WaitlistEntry>> Get(int id)
    {
        var entry = await _db.WaitlistEntries
            .Include(w => w.Customer)
            .Include(w => w.FulfilledGoat)
            .Include(w => w.FulfilledSale)
            .FirstOrDefaultAsync(w => w.Id == id);
        return entry is null ? NotFound() : entry;
    }

    [HttpPost]
    public async Task<ActionResult<WaitlistEntry>> Create(WaitlistEntry entry)
    {
        // Force sane defaults on create — clients shouldn't be able to land
        // already-Fulfilled rows via a plain POST.
        entry.Id = 0;
        entry.Status = WaitlistStatus.Waiting;
        entry.OfferedAt = null;
        entry.FulfilledAt = null;
        entry.FulfilledSaleId = null;
        entry.FulfilledGoatId = null;
        entry.CancelledAt = null;
        entry.CancelReason = null;
        entry.CreatedAt = DateTime.UtcNow;

        if (entry.DepositPaid && entry.DepositReceivedAt is null)
            entry.DepositReceivedAt = DateTime.UtcNow;

        _db.WaitlistEntries.Add(entry);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = entry.Id }, entry);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, WaitlistEntry entry)
    {
        if (id != entry.Id) return BadRequest();
        var existing = await _db.WaitlistEntries.FindAsync(id);
        if (existing is null) return NotFound();
        if (existing.Status is WaitlistStatus.Fulfilled or WaitlistStatus.Cancelled)
            return BadRequest(new { error = "Entry is already finalised and can't be edited." });

        existing.CustomerId = entry.CustomerId;
        existing.BreedPreference = entry.BreedPreference;
        existing.SexPreference = entry.SexPreference;
        existing.ColorPreference = entry.ColorPreference;
        existing.MinDueDate = entry.MinDueDate;
        existing.MaxDueDate = entry.MaxDueDate;
        existing.DepositCents = entry.DepositCents;
        if (entry.DepositPaid && !existing.DepositPaid)
            existing.DepositReceivedAt = DateTime.UtcNow;
        existing.DepositPaid = entry.DepositPaid;
        existing.Priority = entry.Priority;
        existing.Notes = entry.Notes;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entry = await _db.WaitlistEntries.FindAsync(id);
        if (entry is null) return NotFound();
        _db.WaitlistEntries.Remove(entry);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/offer")]
    public async Task<IActionResult> Offer(int id)
    {
        var entry = await _db.WaitlistEntries.FindAsync(id);
        if (entry is null) return NotFound();
        if (entry.Status != WaitlistStatus.Waiting)
            return BadRequest(new { error = $"Can only offer a Waiting entry; this one is {entry.Status}." });

        entry.Status = WaitlistStatus.Offered;
        entry.OfferedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public record FulfillRequest(int GoatId, decimal SaleAmount, string? Description);

    [HttpPost("{id}/fulfill")]
    public async Task<ActionResult<Sale>> Fulfill(int id, [FromBody] FulfillRequest req)
    {
        var entry = await _db.WaitlistEntries.FindAsync(id);
        if (entry is null) return NotFound();
        if (entry.Status is WaitlistStatus.Fulfilled or WaitlistStatus.Cancelled)
            return BadRequest(new { error = $"Entry is already {entry.Status}." });

        var goat = await _db.Goats.FindAsync(req.GoatId);
        if (goat is null) return BadRequest(new { error = "Goat not found in this farm." });

        var deposit = entry.DepositCents / 100m;
        var paymentStatus = entry.DepositPaid && deposit > 0 && deposit < req.SaleAmount
            ? PaymentStatus.Deposited
            : PaymentStatus.PaidInFull;

        var sale = new Sale
        {
            CustomerId = entry.CustomerId,
            SaleType = SaleType.LiveAnimal,
            SaleDate = DateTime.UtcNow,
            Description = req.Description ?? $"Waitlist fulfillment: {goat.Name}",
            Amount = req.SaleAmount,
            DepositAmount = entry.DepositPaid ? deposit : 0m,
            PaymentStatus = paymentStatus,
            GoatId = goat.Id,
            Notes = entry.Notes,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Sales.Add(sale);
        await _db.SaveChangesAsync();

        await SyncLinkedTransactionAsync(sale);

        entry.Status = WaitlistStatus.Fulfilled;
        entry.FulfilledAt = DateTime.UtcNow;
        entry.FulfilledSaleId = sale.Id;
        entry.FulfilledGoatId = goat.Id;
        await _db.SaveChangesAsync();

        return CreatedAtAction("Get", "Sales", new { id = sale.Id }, sale);
    }

    public record CancelRequest(string? Reason);

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelRequest req)
    {
        var entry = await _db.WaitlistEntries.FindAsync(id);
        if (entry is null) return NotFound();
        if (entry.Status is WaitlistStatus.Fulfilled or WaitlistStatus.Cancelled)
            return BadRequest(new { error = $"Entry is already {entry.Status}." });

        entry.Status = WaitlistStatus.Cancelled;
        entry.CancelledAt = DateTime.UtcNow;
        entry.CancelReason = req.Reason;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Mirrors SalesController.SyncLinkedTransactionAsync — pulls the sale into
    // the finance ledger so cost-per-goat picks up waitlist fulfillments.
    private async Task SyncLinkedTransactionAsync(Sale sale)
    {
        var existingTx = await _db.Transactions.FirstOrDefaultAsync(t => t.SaleId == sale.Id);
        var shouldRecord = sale.PaymentStatus is PaymentStatus.PaidInFull or PaymentStatus.Deposited;
        if (!shouldRecord)
        {
            if (existingTx != null) _db.Transactions.Remove(existingTx);
            await _db.SaveChangesAsync();
            return;
        }

        var amount = sale.PaymentStatus == PaymentStatus.Deposited ? sale.DepositAmount : sale.Amount;
        if (existingTx == null)
        {
            _db.Transactions.Add(new Transaction
            {
                Type = TransactionType.Income,
                Date = sale.SaleDate,
                Description = sale.Description ?? "Waitlist fulfillment",
                Amount = amount,
                Category = IncomeCategory.AnimalSale.ToString(),
                GoatId = sale.GoatId,
                SaleId = sale.Id,
                Notes = sale.Notes,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existingTx.Date = sale.SaleDate;
            existingTx.Amount = amount;
        }
        await _db.SaveChangesAsync();
    }
}
