using GoatLab.Server.Data;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchasesController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public PurchasesController(GoatLabDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<Purchase>>> GetAll([FromQuery] int? goatId)
    {
        var query = _db.Purchases
            .Include(p => p.Goat)
            .Include(p => p.Supplier)
            .AsQueryable();
        if (goatId.HasValue) query = query.Where(p => p.GoatId == goatId.Value);
        return await query.OrderByDescending(p => p.PurchaseDate).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Purchase>> Get(int id)
    {
        var purchase = await _db.Purchases
            .Include(p => p.Goat)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == id);
        return purchase is null ? NotFound() : purchase;
    }

    /// <summary>
    /// Body shape supports an inline new-goat flow. If <c>NewGoat</c> is supplied, the goat
    /// is created and linked to the purchase. Otherwise <c>GoatId</c> may point to an existing one.
    /// Always auto-creates a matching expense Transaction.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Purchase>> Create([FromBody] CreatePurchaseRequest req)
    {
        Goat? goat = null;
        if (req.NewGoat != null)
        {
            req.NewGoat.CreatedAt = DateTime.UtcNow;
            req.NewGoat.UpdatedAt = DateTime.UtcNow;
            req.NewGoat.BreederName ??= req.Purchase.SellerName;
            _db.Goats.Add(req.NewGoat);
            await _db.SaveChangesAsync();
            req.Purchase.GoatId = req.NewGoat.Id;
            goat = req.NewGoat;
        }
        else if (req.Purchase.GoatId.HasValue)
        {
            goat = await _db.Goats.FindAsync(req.Purchase.GoatId.Value);
        }

        req.Purchase.CreatedAt = DateTime.UtcNow;
        _db.Purchases.Add(req.Purchase);
        await _db.SaveChangesAsync();

        await SyncLinkedTransactionAsync(req.Purchase);

        return Ok(new { purchase = req.Purchase, goat });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Purchase purchase)
    {
        if (id != purchase.Id) return BadRequest();
        var existing = await _db.Purchases.FindAsync(id);
        if (existing is null) return NotFound();

        existing.PurchaseDate = purchase.PurchaseDate;
        existing.SellerName = purchase.SellerName;
        existing.SupplierId = purchase.SupplierId;
        existing.GoatId = purchase.GoatId;
        existing.Description = purchase.Description;
        existing.Amount = purchase.Amount;
        existing.Notes = purchase.Notes;

        await _db.SaveChangesAsync();
        await SyncLinkedTransactionAsync(existing);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var purchase = await _db.Purchases.FindAsync(id);
        if (purchase is null) return NotFound();

        var linked = await _db.Transactions.Where(t => t.PurchaseId == purchase.Id).ToListAsync();
        if (linked.Any()) _db.Transactions.RemoveRange(linked);

        _db.Purchases.Remove(purchase);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task SyncLinkedTransactionAsync(Purchase purchase)
    {
        var existingTx = await _db.Transactions.FirstOrDefaultAsync(t => t.PurchaseId == purchase.Id);

        if (purchase.Amount <= 0)
        {
            if (existingTx != null) _db.Transactions.Remove(existingTx);
            await _db.SaveChangesAsync();
            return;
        }

        var description = !string.IsNullOrWhiteSpace(purchase.Description)
            ? purchase.Description!
            : purchase.Goat != null
                ? $"Bought {purchase.Goat.Name}"
                : "Goat purchase";

        if (existingTx == null)
        {
            _db.Transactions.Add(new Transaction
            {
                Type = TransactionType.Expense,
                Date = purchase.PurchaseDate,
                Description = description,
                Amount = purchase.Amount,
                Category = ExpenseCategory.Other.ToString(),
                GoatId = purchase.GoatId,
                PurchaseId = purchase.Id,
                Notes = purchase.SellerName != null ? $"Seller: {purchase.SellerName}" : null,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existingTx.Date = purchase.PurchaseDate;
            existingTx.Description = description;
            existingTx.Amount = purchase.Amount;
            existingTx.GoatId = purchase.GoatId;
            existingTx.Notes = purchase.SellerName != null ? $"Seller: {purchase.SellerName}" : null;
        }
        await _db.SaveChangesAsync();
    }
}

public class CreatePurchaseRequest
{
    public Purchase Purchase { get; set; } = new();
    /// <summary>If supplied, a new goat is created from this object and linked to the purchase.</summary>
    public Goat? NewGoat { get; set; }
}
