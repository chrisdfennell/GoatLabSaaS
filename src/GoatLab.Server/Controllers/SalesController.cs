using GoatLab.Server.Data;
using GoatLab.Server.Services.Plans;
using GoatLab.Server.Services.Webhooks;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequiresFeature(AppFeature.Sales)]
public class SalesController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly WebhookDispatcher _webhooks;
    public SalesController(GoatLabDbContext db, WebhookDispatcher webhooks)
    {
        _db = db;
        _webhooks = webhooks;
    }

    private static object SaleSummary(Sale s) => new
    {
        s.Id, s.SaleType, s.SaleDate, s.Amount, s.DepositAmount, s.PaymentStatus,
        s.CustomerId, s.GoatId, s.Description
    };

    // --- Sales ---

    [HttpGet]
    public async Task<ActionResult<List<Sale>>> GetAll(
        [FromQuery] PaymentStatus? status,
        [FromQuery] SaleType? type,
        [FromQuery] int? goatId,
        [FromQuery] int? customerId)
    {
        var query = _db.Sales
            .Include(s => s.Customer)
            .Include(s => s.Goat)
            .AsQueryable();

        if (status.HasValue) query = query.Where(s => s.PaymentStatus == status.Value);
        if (type.HasValue) query = query.Where(s => s.SaleType == type.Value);
        if (goatId.HasValue) query = query.Where(s => s.GoatId == goatId.Value);
        if (customerId.HasValue) query = query.Where(s => s.CustomerId == customerId.Value);

        return await query.OrderByDescending(s => s.SaleDate).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Sale>> Get(int id)
    {
        var sale = await _db.Sales
            .Include(s => s.Customer)
            .Include(s => s.Goat)
            .FirstOrDefaultAsync(s => s.Id == id);
        return sale is null ? NotFound() : sale;
    }

    [HttpPost]
    public async Task<ActionResult<Sale>> Create(Sale sale)
    {
        sale.CreatedAt = DateTime.UtcNow;
        _db.Sales.Add(sale);
        await _db.SaveChangesAsync();
        await SyncLinkedTransactionAsync(sale);
        await _webhooks.DispatchAsync(WebhookEventTypes.SaleCreated, SaleSummary(sale));
        return CreatedAtAction(nameof(Get), new { id = sale.Id }, sale);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Sale sale)
    {
        if (id != sale.Id) return BadRequest();
        var existing = await _db.Sales.FindAsync(id);
        if (existing is null) return NotFound();

        existing.CustomerId = sale.CustomerId;
        existing.SaleType = sale.SaleType;
        existing.SaleDate = sale.SaleDate;
        existing.Description = sale.Description;
        existing.Amount = sale.Amount;
        existing.DepositAmount = sale.DepositAmount;
        existing.PaymentStatus = sale.PaymentStatus;
        existing.GoatId = sale.GoatId;
        existing.Notes = sale.Notes;

        await _db.SaveChangesAsync();
        await SyncLinkedTransactionAsync(existing);
        await _webhooks.DispatchAsync(WebhookEventTypes.SaleUpdated, SaleSummary(existing));
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var sale = await _db.Sales.FindAsync(id);
        if (sale is null) return NotFound();

        // Remove any linked finance transaction first
        var linked = await _db.Transactions.Where(t => t.SaleId == sale.Id).ToListAsync();
        if (linked.Any()) _db.Transactions.RemoveRange(linked);

        _db.Sales.Remove(sale);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Mirrors the sale into the Transactions table so the finance dashboard & cost-per-goat
    // see revenue. If the sale is Refunded/Cancelled, the linked transaction is removed.
    private async Task SyncLinkedTransactionAsync(Sale sale)
    {
        var existingTx = await _db.Transactions.FirstOrDefaultAsync(t => t.SaleId == sale.Id);

        var shouldRecordIncome = sale.PaymentStatus is PaymentStatus.PaidInFull or PaymentStatus.Deposited;
        if (!shouldRecordIncome)
        {
            if (existingTx != null) _db.Transactions.Remove(existingTx);
            await _db.SaveChangesAsync();
            return;
        }

        var amount = sale.PaymentStatus == PaymentStatus.Deposited ? sale.DepositAmount : sale.Amount;
        var category = sale.SaleType switch
        {
            SaleType.LiveAnimal => IncomeCategory.AnimalSale,
            SaleType.Milk => IncomeCategory.MilkSale,
            SaleType.Meat => IncomeCategory.MeatSale,
            SaleType.Breeding => IncomeCategory.BreedingFee,
            _ => IncomeCategory.Other
        };

        if (existingTx == null)
        {
            _db.Transactions.Add(new Transaction
            {
                Type = TransactionType.Income,
                Date = sale.SaleDate,
                Description = sale.Description ?? $"{sale.SaleType} sale",
                Amount = amount,
                Category = category.ToString(),
                GoatId = sale.GoatId,
                SaleId = sale.Id,
                Notes = sale.Notes,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existingTx.Date = sale.SaleDate;
            existingTx.Description = sale.Description ?? $"{sale.SaleType} sale";
            existingTx.Amount = amount;
            existingTx.Category = category.ToString();
            existingTx.GoatId = sale.GoatId;
            existingTx.Notes = sale.Notes;
        }
        await _db.SaveChangesAsync();
    }

    // --- Customers ---

    [HttpGet("customers")]
    public async Task<ActionResult<List<Customer>>> GetCustomers([FromQuery] bool? waitingList)
    {
        var query = _db.Customers.Include(c => c.Sales).AsQueryable();
        if (waitingList.HasValue) query = query.Where(c => c.IsOnWaitingList == waitingList.Value);
        return await query.OrderBy(c => c.Name).ToListAsync();
    }

    [HttpGet("customers/{id}")]
    public async Task<ActionResult<Customer>> GetCustomer(int id)
    {
        var customer = await _db.Customers
            .Include(c => c.Sales).ThenInclude(s => s.Goat)
            .FirstOrDefaultAsync(c => c.Id == id);
        return customer is null ? NotFound() : customer;
    }

    [HttpPost("customers")]
    public async Task<ActionResult<Customer>> CreateCustomer(Customer customer)
    {
        customer.CreatedAt = DateTime.UtcNow;
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customer);
    }

    [HttpPut("customers/{id}")]
    public async Task<IActionResult> UpdateCustomer(int id, Customer customer)
    {
        if (id != customer.Id) return BadRequest();
        var existing = await _db.Customers.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = customer.Name;
        existing.Email = customer.Email;
        existing.Phone = customer.Phone;
        existing.Address = customer.Address;
        existing.Notes = customer.Notes;
        existing.IsOnWaitingList = customer.IsOnWaitingList;
        existing.WaitingListNotes = customer.WaitingListNotes;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("customers/{id}")]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return NotFound();
        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
