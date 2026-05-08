using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using GoatLab.Server.Filters;
using GoatLab.Server.Services;
using GoatLab.Server.Services.Email;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Buyer ↔ seller messaging on public goat listings.
//
// Two surfaces:
//   1. Anonymous buyer (no account) sends a message via /api/public/inquiries.
//      We resolve the farm by slug, validate the goat is publicly listed,
//      then create-or-find an inquiry keyed by (tenant, goat, buyerEmail) so
//      follow-up messages append to one thread instead of cluttering the inbox.
//   2. Authenticated seller sees /api/inquiries (their tenant's threads), reads
//      individual threads, replies (which emails the buyer), closes threads.
//
// reCAPTCHA covers the public POST surface; the rest is gated by cookie auth +
// the standard ITenantOwned global filter.
[ApiController]
[RequiresSaas]
public class BuyerInquiriesController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAppEmailSender _email;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<BuyerInquiriesController> _log;

    public BuyerInquiriesController(
        GoatLabDbContext db,
        ITenantContext tenantContext,
        IAppEmailSender email,
        UserManager<ApplicationUser> userManager,
        ILogger<BuyerInquiriesController> log)
    {
        _db = db;
        _tenantContext = tenantContext;
        _email = email;
        _userManager = userManager;
        _log = log;
    }

    public record SendInquiryRequest(string BuyerName, string BuyerEmail, string? BuyerPhone, string Message);
    public record InquiryResponse(int Id, string Message);

    // ===================== Anonymous (buyer) =====================

    [HttpPost("/api/public/farms/{slug}/goats/{goatId:int}/inquire")]
    [AllowAnonymous]
    [RequireRecaptcha("buyer_inquiry")]
    public async Task<ActionResult<InquiryResponse>> Send(
        string slug, int goatId, [FromBody] SendInquiryRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.BuyerEmail) || !req.BuyerEmail.Contains('@'))
            return BadRequest(new { error = "A valid email is required." });
        if (string.IsNullOrWhiteSpace(req.BuyerName))
            return BadRequest(new { error = "Please tell the seller your name." });
        if (string.IsNullOrWhiteSpace(req.Message) || req.Message.Length < 5)
            return BadRequest(new { error = "Please write a short message about what you're looking for." });
        if (req.Message.Length > 4000)
            return BadRequest(new { error = "Message is too long (max 4000 chars)." });

        // Cross-tenant lookup — anon has no tenant claim. Bypass the filter
        // for tenant + goat resolution; once we have the matched IDs the rest
        // of the writes scope by them.
        _tenantContext.BypassFilter = true;

        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == slug
                                   && t.PublicProfileEnabled
                                   && t.DeletedAt == null
                                   && t.SuspendedAt == null, ct);
        if (tenant is null) return NotFound();

        var goat = await _db.Goats.IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Id == goatId
                                   && g.TenantId == tenant.Id
                                   && g.IsListedForSale, ct);
        if (goat is null) return NotFound();

        var emailNorm = req.BuyerEmail.Trim().ToLowerInvariant();

        var inquiry = await _db.BuyerInquiries.IgnoreQueryFilters()
            .Include(i => i.Messages)
            .FirstOrDefaultAsync(i => i.TenantId == tenant.Id
                                   && i.GoatId == goat.Id
                                   && i.BuyerEmail == emailNorm, ct);

        var now = DateTime.UtcNow;
        if (inquiry is null)
        {
            inquiry = new BuyerInquiry
            {
                TenantId = tenant.Id,
                GoatId = goat.Id,
                BuyerEmail = emailNorm,
                BuyerName = req.BuyerName.Trim(),
                BuyerPhone = string.IsNullOrWhiteSpace(req.BuyerPhone) ? null : req.BuyerPhone.Trim(),
                Status = BuyerInquiryStatus.Open,
                CreatedAt = now,
                LastMessageAt = now,
                UnreadForSeller = true,
            };
            _db.BuyerInquiries.Add(inquiry);
        }
        else
        {
            // Refresh contact details if the buyer typed a different name/phone
            // on a follow-up — the latest values are usually what the seller
            // wants to see in the inbox.
            inquiry.BuyerName = req.BuyerName.Trim();
            if (!string.IsNullOrWhiteSpace(req.BuyerPhone)) inquiry.BuyerPhone = req.BuyerPhone.Trim();
            inquiry.Status = BuyerInquiryStatus.Open;
            inquiry.LastMessageAt = now;
            inquiry.UnreadForSeller = true;
        }

        _db.BuyerInquiryMessages.Add(new BuyerInquiryMessage
        {
            Inquiry = inquiry,
            FromSeller = false,
            Body = req.Message.Trim(),
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(ct);

        // Notify the seller. Prefer the tenant's PublicContactEmail; fall back
        // to any owner-role member's email so the inquiry doesn't disappear if
        // the public contact wasn't filled in.
        string? notifyEmail = tenant.PublicContactEmail;
        if (string.IsNullOrWhiteSpace(notifyEmail))
        {
            var ownerUserId = await _db.TenantMembers.IgnoreQueryFilters()
                .Where(m => m.TenantId == tenant.Id && m.Role == TenantRole.Owner)
                .OrderBy(m => m.JoinedAt)
                .Select(m => m.UserId)
                .FirstOrDefaultAsync(ct);
            if (ownerUserId is not null)
            {
                var owner = await _userManager.FindByIdAsync(ownerUserId);
                if (owner?.DeletedAt is null) notifyEmail = owner?.Email;
            }
        }

        if (!string.IsNullOrWhiteSpace(notifyEmail))
        {
            try
            {
                var origin = $"{Request.Scheme}://{Request.Host}";
                var inboxUrl = $"{origin}/inquiries";
                var (subject, html, text) = EmailTemplates.BuyerInquiryNew(
                    inquiry.BuyerName, inquiry.BuyerEmail, inquiry.BuyerPhone,
                    goat.Name, tenant.Name, req.Message.Trim(), inboxUrl);
                await _email.SendAsync(notifyEmail!, subject, html, text, ct);
            }
            catch (Exception ex)
            {
                // Don't fail the request — the inquiry IS saved and the seller
                // can see it on their next inbox load. But log loudly so ops
                // notice when SMTP is broken; otherwise inquiries silently
                // pile up while the seller wonders why nobody's contacting them.
                _log.LogError(ex,
                    "Failed to send buyer-inquiry notification to {NotifyEmail} for inquiry {InquiryId} on goat {GoatId}",
                    notifyEmail, inquiry.Id, goat.Id);
            }
        }

        return new InquiryResponse(inquiry.Id, "Sent! The seller will get back to you by email.");
    }

    // ===================== Authenticated (seller) =====================

    public record InquiryListItem(
        int Id, int GoatId, string GoatName, string BuyerName, string BuyerEmail, string? BuyerPhone,
        string Status, bool UnreadForSeller, DateTime CreatedAt, DateTime LastMessageAt, int MessageCount);

    public record InquiryMessageDto(int Id, bool FromSeller, string Body, DateTime CreatedAt);
    public record InquiryDetailDto(
        int Id, int GoatId, string GoatName, string? GoatEarTag,
        string BuyerName, string BuyerEmail, string? BuyerPhone,
        string Status, DateTime CreatedAt, DateTime LastMessageAt,
        IReadOnlyList<InquiryMessageDto> Messages);

    public record ReplyRequest(string Message);

    [HttpGet("/api/inquiries")]
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    public async Task<ActionResult<IReadOnlyList<InquiryListItem>>> List(CancellationToken ct)
    {
        if (_tenantContext.TenantId is null) return Unauthorized();

        var rows = await _db.BuyerInquiries
            .Include(i => i.Goat)
            .OrderByDescending(i => i.LastMessageAt)
            .Take(200)
            .Select(i => new InquiryListItem(
                i.Id, i.GoatId, i.Goat!.Name, i.BuyerName, i.BuyerEmail, i.BuyerPhone,
                i.Status.ToString(), i.UnreadForSeller, i.CreatedAt, i.LastMessageAt,
                i.Messages.Count))
            .ToListAsync(ct);
        return rows;
    }

    [HttpGet("/api/inquiries/unread-count")]
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    public async Task<ActionResult<int>> UnreadCount(CancellationToken ct)
    {
        if (_tenantContext.TenantId is null) return Unauthorized();
        return await _db.BuyerInquiries.CountAsync(i => i.UnreadForSeller, ct);
    }

    [HttpGet("/api/inquiries/{id:int}")]
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    public async Task<ActionResult<InquiryDetailDto>> Get(int id, CancellationToken ct)
    {
        if (_tenantContext.TenantId is null) return Unauthorized();

        var inq = await _db.BuyerInquiries
            .Include(i => i.Goat)
            .Include(i => i.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        if (inq is null) return NotFound();

        // Auto-mark read on detail open — saves a round-trip and matches
        // every other "open the thread, badge goes away" inbox in the world.
        if (inq.UnreadForSeller)
        {
            inq.UnreadForSeller = false;
            await _db.SaveChangesAsync(ct);
        }

        return new InquiryDetailDto(
            inq.Id, inq.GoatId, inq.Goat!.Name, inq.Goat.EarTag,
            inq.BuyerName, inq.BuyerEmail, inq.BuyerPhone,
            inq.Status.ToString(), inq.CreatedAt, inq.LastMessageAt,
            inq.Messages
                .OrderBy(m => m.CreatedAt)
                .Select(m => new InquiryMessageDto(m.Id, m.FromSeller, m.Body, m.CreatedAt))
                .ToList());
    }

    [HttpPost("/api/inquiries/{id:int}/reply")]
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    public async Task<IActionResult> Reply(int id, [FromBody] ReplyRequest req, CancellationToken ct)
    {
        if (_tenantContext.TenantId is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.Message)) return BadRequest(new { error = "Reply cannot be empty." });
        if (req.Message.Length > 4000) return BadRequest(new { error = "Reply is too long (max 4000 chars)." });

        var inq = await _db.BuyerInquiries
            .Include(i => i.Goat)
            .Include(i => i.Tenant)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        if (inq is null) return NotFound();

        var now = DateTime.UtcNow;
        var message = new BuyerInquiryMessage
        {
            InquiryId = inq.Id,
            FromSeller = true,
            Body = req.Message.Trim(),
            CreatedAt = now,
        };
        _db.BuyerInquiryMessages.Add(message);

        inq.LastMessageAt = now;
        inq.UnreadForSeller = false;
        if (inq.Status == BuyerInquiryStatus.Closed) inq.Status = BuyerInquiryStatus.Open;

        await _db.SaveChangesAsync(ct);

        try
        {
            var seller = await _userManager.GetUserAsync(User);
            var sellerName = seller?.DisplayName ?? inq.Tenant?.Name ?? "Your seller";
            var origin = $"{Request.Scheme}://{Request.Host}";
            var listingUrl = $"{origin}/pub/{inq.Tenant?.Slug}/{inq.GoatId}";
            var (subject, html, text) = EmailTemplates.BuyerInquiryReply(
                inq.BuyerName, sellerName, inq.Tenant?.Name ?? "GoatLab",
                inq.Goat?.Name ?? "your inquiry", req.Message.Trim(), listingUrl);
            await _email.SendAsync(inq.BuyerEmail, subject, html, text, ct);
        }
        catch
        {
            // Already saved — don't fail the seller's request because Brevo
            // had a hiccup. Email log will show the failure.
        }
        return Ok(new { id = message.Id });
    }

    [HttpPost("/api/inquiries/{id:int}/close")]
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    public async Task<IActionResult> Close(int id, CancellationToken ct)
    {
        if (_tenantContext.TenantId is null) return Unauthorized();
        var inq = await _db.BuyerInquiries.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (inq is null) return NotFound();
        inq.Status = BuyerInquiryStatus.Closed;
        inq.UnreadForSeller = false;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("/api/inquiries/{id:int}/reopen")]
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    public async Task<IActionResult> Reopen(int id, CancellationToken ct)
    {
        if (_tenantContext.TenantId is null) return Unauthorized();
        var inq = await _db.BuyerInquiries.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (inq is null) return NotFound();
        inq.Status = BuyerInquiryStatus.Open;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
