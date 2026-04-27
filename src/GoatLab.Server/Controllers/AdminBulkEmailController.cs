using GoatLab.Server.Data;
using GoatLab.Server.Services;
using GoatLab.Server.Services.Email;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Super-admin "broadcast a message to my customers" tool. Audiences map to
// SQL filters over Tenants -> TenantMembers (Owner role). DryRun returns the
// recipient count without sending so the admin can confirm before firing.
//
// IMPORTANT: this is *operational* email — "we're moving servers Sunday at
// 2am" — and is intentionally NOT gated by Tenant.AlertEmailEnabled (which
// gates the alert digest, not service-critical announcements). Don't use
// this for marketing without first adding a separate marketing-opt-in flag.
[ApiController]
[Route("api/admin/bulk-email")]
[Authorize(Policy = SuperAdminPolicy.Name)]
public class AdminBulkEmailController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly IAppEmailSender _email;
    private readonly IAdminAuditLogger _audit;
    private readonly ILogger<AdminBulkEmailController> _logger;

    public AdminBulkEmailController(
        GoatLabDbContext db,
        IAppEmailSender email,
        IAdminAuditLogger audit,
        ILogger<AdminBulkEmailController> logger)
    {
        _db = db;
        _email = email;
        _audit = audit;
        _logger = logger;
    }

    public record PreviewResult(string Subject, string Html);

    /// <summary>
    /// Renders the wrapped template against a fake recipient ("Sample Owner")
    /// so the admin can see exactly what customers will receive before pulling
    /// the trigger. No data fetched; same template used by the Send path.
    /// </summary>
    [HttpPost("preview")]
    public ActionResult<PreviewResult> Preview([FromBody] BulkEmailRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Subject) || string.IsNullOrWhiteSpace(req.HtmlBody))
            return BadRequest("Subject and message body are required.");

        var (subject, html, _) = EmailTemplates.BulkAnnouncement(
            displayName: "Sample Owner",
            subject: req.Subject,
            messageBody: req.HtmlBody,
            preheader: req.Preheader);
        return new PreviewResult(subject, html);
    }

    [HttpPost]
    public async Task<ActionResult<BulkEmailResultDto>> Send([FromBody] BulkEmailRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Subject) || string.IsNullOrWhiteSpace(req.HtmlBody))
            return BadRequest("Subject and HtmlBody are required.");
        if (string.IsNullOrWhiteSpace(req.Audience))
            return BadRequest("Audience is required.");

        var recipients = await ResolveAudienceAsync(req.Audience, ct);
        if (recipients.Count == 0)
            return new BulkEmailResultDto(req.Audience, 0, req.DryRun, 0, 0, Array.Empty<string>());

        if (req.DryRun)
        {
            return new BulkEmailResultDto(req.Audience, recipients.Count, true, 0, 0,
                recipients.Take(10).Select(r => r.Email).ToList());
        }

        var sent = 0;
        var failed = 0;
        foreach (var r in recipients)
        {
            try
            {
                // Wrap the operator-supplied content in branded chrome (header
                // bar, greeting personalized to the recipient, footer with
                // links). Keeps every announcement looking like a real GoatLab
                // email instead of a raw paste.
                var (subject, html, text) = EmailTemplates.BulkAnnouncement(
                    displayName: r.Name,
                    subject: req.Subject,
                    messageBody: req.HtmlBody,
                    preheader: req.Preheader);
                await _email.SendAsync(r.Email, subject, html, text, ct);
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bulk email failed for {Email}", r.Email);
                failed++;
            }
        }

        await _audit.LogAsync(
            action: "bulk-email.send",
            targetType: "Audience",
            targetId: req.Audience,
            detail: $"\"{req.Subject}\" — {sent} sent, {failed} failed of {recipients.Count}");

        return new BulkEmailResultDto(req.Audience, recipients.Count, false, sent, failed,
            recipients.Take(10).Select(r => r.Email).ToList());
    }

    public record Recipient(string Email, string Name);

    // Audience selectors. Always filters out soft-deleted/suspended tenants.
    // Resolves to Owner-role members so we don't email every Viewer too.
    // TenantMember has no User navigation property — we manually join to the
    // identity Users table by UserId.
    private async Task<List<Recipient>> ResolveAudienceAsync(string audience, CancellationToken ct)
    {
        var baseQ =
            from tm in _db.TenantMembers.IgnoreQueryFilters()
            join u in _db.Users on tm.UserId equals u.Id
            where tm.Role == TenantRole.Owner
                  && tm.Tenant!.DeletedAt == null
                  && tm.Tenant.SuspendedAt == null
                  && u.DeletedAt == null
                  && u.Email != null
            select new { Tenant = tm.Tenant, User = u };

        var filtered = audience.Trim().ToLowerInvariant() switch
        {
            "all-owners" => baseQ,
            "active-paid" => baseQ.Where(x => x.Tenant!.SubscriptionStatus == "active"),
            "trial" => baseQ.Where(x => x.Tenant!.SubscriptionStatus == "trialing"),
            "past-due" => baseQ.Where(x => x.Tenant!.SubscriptionStatus == "past_due"
                                        || x.Tenant.SubscriptionStatus == "unpaid"),
            _ => null
        };
        if (filtered is null)
            throw new ArgumentException($"Unknown audience: {audience}");

        // Distinct by email since one user can own multiple tenants.
        var rows = await filtered
            .Select(x => new { x.User.Email, x.User.DisplayName })
            .ToListAsync(ct);

        return rows
            .Where(r => !string.IsNullOrEmpty(r.Email))
            .GroupBy(r => r.Email!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new Recipient(g.Key, g.First().DisplayName ?? ""))
            .ToList();
    }
}
