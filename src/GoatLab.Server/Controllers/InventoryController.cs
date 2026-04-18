using GoatLab.Server.Data;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequiresFeature(AppFeature.Inventory)]
public class InventoryController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public InventoryController(GoatLabDbContext db) => _db = db;

    // --- Suppliers ---

    [HttpGet("suppliers")]
    public async Task<ActionResult<List<Supplier>>> GetSuppliers([FromQuery] SupplierType? type)
    {
        var query = _db.Suppliers.AsQueryable();
        if (type.HasValue) query = query.Where(s => s.SupplierType == type.Value);
        return await query.OrderBy(s => s.Name).ToListAsync();
    }

    [HttpGet("suppliers/{id}")]
    public async Task<ActionResult<Supplier>> GetSupplier(int id)
    {
        var supplier = await _db.Suppliers.FindAsync(id);
        return supplier is null ? NotFound() : supplier;
    }

    [HttpPost("suppliers")]
    public async Task<ActionResult<Supplier>> CreateSupplier(Supplier supplier)
    {
        supplier.CreatedAt = DateTime.UtcNow;
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetSupplier), new { id = supplier.Id }, supplier);
    }

    [HttpPut("suppliers/{id}")]
    public async Task<IActionResult> UpdateSupplier(int id, Supplier supplier)
    {
        if (id != supplier.Id) return BadRequest();
        var existing = await _db.Suppliers.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = supplier.Name;
        existing.SupplierType = supplier.SupplierType;
        existing.ContactName = supplier.ContactName;
        existing.Phone = supplier.Phone;
        existing.Email = supplier.Email;
        existing.Address = supplier.Address;
        existing.Website = supplier.Website;
        existing.Notes = supplier.Notes;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("suppliers/{id}")]
    public async Task<IActionResult> DeleteSupplier(int id)
    {
        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier is null) return NotFound();
        _db.Suppliers.Remove(supplier);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Feed Inventory ---

    [HttpGet("feed")]
    public async Task<ActionResult<List<FeedInventory>>> GetFeed()
    {
        return await _db.FeedInventory
            .Include(f => f.Supplier)
            .OrderBy(f => f.FeedName)
            .ToListAsync();
    }

    [HttpGet("feed/low-stock")]
    public async Task<ActionResult<List<FeedInventory>>> GetLowStock()
    {
        return await _db.FeedInventory
            .Include(f => f.Supplier)
            .Where(f => f.LowStockThreshold != null && f.QuantityOnHand <= f.LowStockThreshold)
            .OrderBy(f => f.QuantityOnHand)
            .ToListAsync();
    }

    [HttpPost("feed")]
    public async Task<ActionResult<FeedInventory>> CreateFeed(FeedInventory feed)
    {
        feed.LastUpdated = DateTime.UtcNow;
        _db.FeedInventory.Add(feed);
        await _db.SaveChangesAsync();
        return Ok(feed);
    }

    [HttpPut("feed/{id}")]
    public async Task<IActionResult> UpdateFeed(int id, FeedInventory feed)
    {
        if (id != feed.Id) return BadRequest();
        var existing = await _db.FeedInventory.FindAsync(id);
        if (existing is null) return NotFound();

        existing.FeedName = feed.FeedName;
        existing.QuantityOnHand = feed.QuantityOnHand;
        existing.Unit = feed.Unit;
        existing.LowStockThreshold = feed.LowStockThreshold;
        existing.CostPerUnit = feed.CostPerUnit;
        existing.SupplierId = feed.SupplierId;
        existing.ExpirationDate = feed.ExpirationDate;
        existing.LotNumber = feed.LotNumber;
        existing.Notes = feed.Notes;
        existing.LastUpdated = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("feed/{id}")]
    public async Task<IActionResult> DeleteFeed(int id)
    {
        var feed = await _db.FeedInventory.FindAsync(id);
        if (feed is null) return NotFound();
        _db.FeedInventory.Remove(feed);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Feed Consumption ---

    [HttpGet("feed-consumption")]
    public async Task<ActionResult<List<FeedConsumption>>> GetConsumption(
        [FromQuery] int? feedId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var q = _db.FeedConsumptions
            .Include(c => c.FeedInventory)
            .AsQueryable();
        if (feedId.HasValue) q = q.Where(c => c.FeedInventoryId == feedId.Value);
        if (from.HasValue) q = q.Where(c => c.Date >= from.Value);
        if (to.HasValue) q = q.Where(c => c.Date <= to.Value);
        return await q.OrderByDescending(c => c.Date).ToListAsync();
    }

    [HttpPost("feed-consumption")]
    public async Task<ActionResult<FeedConsumption>> LogConsumption(FeedConsumption log)
    {
        var feed = await _db.FeedInventory.FindAsync(log.FeedInventoryId);
        if (feed is null) return NotFound(new { error = "Feed item not found." });
        if (log.Quantity <= 0) return BadRequest(new { error = "Quantity must be positive." });

        log.CreatedAt = DateTime.UtcNow;
        _db.FeedConsumptions.Add(log);
        feed.QuantityOnHand = Math.Max(0, feed.QuantityOnHand - log.Quantity);
        feed.LastUpdated = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(log);
    }

    public record BulkFeedConsumptionDto(DateTime Date, string? Notes, List<BulkFeedConsumptionItem> Items);
    public record BulkFeedConsumptionItem(int FeedInventoryId, double Quantity);

    [HttpPost("feed-consumption/bulk")]
    public async Task<ActionResult<object>> LogConsumptionBulk(BulkFeedConsumptionDto bulk)
    {
        if (bulk.Items.Count == 0) return BadRequest(new { error = "No items provided." });

        var ids = bulk.Items.Select(i => i.FeedInventoryId).Distinct().ToList();
        var items = await _db.FeedInventory.Where(f => ids.Contains(f.Id)).ToDictionaryAsync(f => f.Id);
        var missing = ids.Where(id => !items.ContainsKey(id)).ToList();
        if (missing.Count > 0) return NotFound(new { error = $"Feed items not found: {string.Join(", ", missing)}" });

        var created = new List<FeedConsumption>();
        var now = DateTime.UtcNow;
        foreach (var row in bulk.Items.Where(i => i.Quantity > 0))
        {
            var feed = items[row.FeedInventoryId];
            var log = new FeedConsumption
            {
                FeedInventoryId = row.FeedInventoryId,
                Date = bulk.Date,
                Quantity = row.Quantity,
                Notes = bulk.Notes,
                CreatedAt = now
            };
            _db.FeedConsumptions.Add(log);
            feed.QuantityOnHand = Math.Max(0, feed.QuantityOnHand - row.Quantity);
            feed.LastUpdated = now;
            created.Add(log);
        }
        await _db.SaveChangesAsync();
        return Ok(new { logged = created.Count });
    }

    [HttpDelete("feed-consumption/{id}")]
    public async Task<IActionResult> DeleteConsumption(int id)
    {
        var log = await _db.FeedConsumptions.FindAsync(id);
        if (log is null) return NotFound();
        var feed = await _db.FeedInventory.FindAsync(log.FeedInventoryId);
        if (feed != null)
        {
            feed.QuantityOnHand += log.Quantity;
            feed.LastUpdated = DateTime.UtcNow;
        }
        _db.FeedConsumptions.Remove(log);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Medicine Cabinet ---

    [HttpGet("medicine")]
    public async Task<ActionResult<List<MedicineCabinetItem>>> GetMedicineCabinet()
    {
        return await _db.MedicineCabinetItems
            .Include(m => m.Medication)
            .OrderBy(m => m.Medication.Name)
            .ToListAsync();
    }

    [HttpPost("medicine")]
    public async Task<ActionResult<MedicineCabinetItem>> AddCabinetItem(MedicineCabinetItem item)
    {
        item.LastUpdated = DateTime.UtcNow;
        _db.MedicineCabinetItems.Add(item);
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpPut("medicine/{id}")]
    public async Task<IActionResult> UpdateCabinetItem(int id, MedicineCabinetItem item)
    {
        if (id != item.Id) return BadRequest();
        var existing = await _db.MedicineCabinetItems.FindAsync(id);
        if (existing is null) return NotFound();

        existing.MedicationId = item.MedicationId;
        existing.Quantity = item.Quantity;
        existing.Unit = item.Unit;
        existing.ExpirationDate = item.ExpirationDate;
        existing.LotNumber = item.LotNumber;
        existing.LastUpdated = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("medicine/{id}")]
    public async Task<IActionResult> DeleteCabinetItem(int id)
    {
        var item = await _db.MedicineCabinetItems.FindAsync(id);
        if (item is null) return NotFound();
        _db.MedicineCabinetItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Expiration alerts: meds + feed in one list ---

    [HttpGet("expiring")]
    public async Task<ActionResult<object>> GetExpiring([FromQuery] int days = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(days);
        var now = DateTime.UtcNow;

        var meds = await _db.MedicineCabinetItems
            .Include(m => m.Medication)
            .Where(m => m.ExpirationDate != null && m.ExpirationDate <= cutoff)
            .OrderBy(m => m.ExpirationDate)
            .Select(m => new
            {
                kind = "medicine",
                id = m.Id,
                name = m.Medication.Name,
                quantity = m.Quantity,
                unit = m.Unit,
                lotNumber = m.LotNumber,
                expirationDate = m.ExpirationDate,
                expired = m.ExpirationDate < now
            })
            .ToListAsync();

        var feed = await _db.FeedInventory
            .Where(f => f.ExpirationDate != null && f.ExpirationDate <= cutoff)
            .OrderBy(f => f.ExpirationDate)
            .Select(f => new
            {
                kind = "feed",
                id = f.Id,
                name = f.FeedName,
                quantity = f.QuantityOnHand,
                unit = f.Unit,
                lotNumber = f.LotNumber,
                expirationDate = f.ExpirationDate,
                expired = f.ExpirationDate < now
            })
            .ToListAsync();

        return Ok(meds.Concat(feed).OrderBy(x => x.expirationDate));
    }
}
