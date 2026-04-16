using GoatLab.Server.Data;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
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
