using System.Security.Cryptography;
using GoatLab.Server.Data;
using GoatLab.Server.Services.ApiKeys;
using GoatLab.Server.Services.Email;
using GoatLab.Server.Services.Plans;
using GoatLab.Server.Services.Webhooks;
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
    private readonly WebhookDispatcher? _webhooks;
    private readonly IAppEmailSender _email;
    public WaitlistController(GoatLabDbContext db, IAppEmailSender email, WebhookDispatcher? webhooks = null)
    {
        _db = db;
        _email = email;
        _webhooks = webhooks;
    }

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

        if (_webhooks is not null)
        {
            await _webhooks.DispatchAsync(WebhookEventTypes.SaleCreated, new
            {
                sale.Id, sale.SaleType, sale.Amount, sale.DepositAmount,
                sale.PaymentStatus, sale.CustomerId, sale.GoatId, source = "waitlist",
            });
        }

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

    // --- Buyer portal magic-link tokens ---

    public record IssuePortalTokenRequest(int DaysValid = 90, bool SendEmail = true);
    public record IssuePortalTokenResponse(string Token, string Url, DateTime ExpiresAt, bool EmailSent);
    public record PortalTokenDto(int Id, string Prefix, DateTime CreatedAt, DateTime ExpiresAt, DateTime? LastUsedAt, DateTime? RevokedAt);

    [HttpPost("{id}/portal-link")]
    public async Task<ActionResult<IssuePortalTokenResponse>> IssuePortalToken(int id, IssuePortalTokenRequest req)
    {
        var entry = await _db.WaitlistEntries
            .Include(w => w.Customer)
            .Include(w => w.Tenant)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (entry is null) return NotFound();
        if (entry.Customer is null) return BadRequest(new { error = "Waitlist entry has no customer." });

        // Token: portal_<32 random bytes base64url>. Same hashing story as API
        // keys — we store SHA-256 only, plaintext is shown/emailed once.
        var bytes = RandomNumberGenerator.GetBytes(32);
        var plaintext = "portal_" + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var hash = ApiKeyGenerator.HashPlaintext(plaintext);
        var days = Math.Clamp(req.DaysValid, 1, 365);
        var expiresAt = DateTime.UtcNow.AddDays(days);

        var token = new BuyerAccessToken
        {
            WaitlistEntryId = entry.Id,
            TokenHash = hash,
            Prefix = plaintext[..Math.Min(12, plaintext.Length)],
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
        };
        _db.BuyerAccessTokens.Add(token);
        await _db.SaveChangesAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var url = $"{baseUrl}/buyer/{plaintext}";

        bool emailSent = false;
        if (req.SendEmail && !string.IsNullOrWhiteSpace(entry.Customer.Email))
        {
            var tpl = EmailTemplates.BuyerPortal(
                entry.Customer.Name,
                entry.Tenant?.Name ?? "our farm",
                url,
                expiresAt);
            await _email.SendAsync(entry.Customer.Email!, tpl.Subject, tpl.Html, tpl.Text);
            emailSent = true;
        }

        return Ok(new IssuePortalTokenResponse(plaintext, url, expiresAt, emailSent));
    }

    [HttpGet("{id}/portal-links")]
    public async Task<ActionResult<List<PortalTokenDto>>> GetPortalTokens(int id)
    {
        var tokens = await _db.BuyerAccessTokens
            .Where(t => t.WaitlistEntryId == id)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new PortalTokenDto(t.Id, t.Prefix, t.CreatedAt, t.ExpiresAt, t.LastUsedAt, t.RevokedAt))
            .ToListAsync();
        return tokens;
    }

    [HttpDelete("portal-links/{tokenId}")]
    public async Task<IActionResult> RevokePortalToken(int tokenId)
    {
        var token = await _db.BuyerAccessTokens.FindAsync(tokenId);
        if (token is null) return NotFound();
        if (token.RevokedAt is null)
        {
            token.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
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
