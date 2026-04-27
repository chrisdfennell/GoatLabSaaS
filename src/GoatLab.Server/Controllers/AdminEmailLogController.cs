using GoatLab.Server.Data;
using GoatLab.Server.Services;
using GoatLab.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// "Did the password reset email send?" — answers that without grepping logs
// or opening the Brevo dashboard. Reads from the EmailLog table that
// LoggingEmailSenderDecorator populates on every send attempt.
[ApiController]
[Route("api/admin/email-log")]
[Authorize(Policy = SuperAdminPolicy.Name)]
public class AdminEmailLogController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public AdminEmailLogController(GoatLabDbContext db) => _db = db;

    [HttpGet]
    public async Task<EmailLogPageDto> List(
        [FromQuery] string? recipient,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var q = _db.EmailLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(recipient))
            q = q.Where(e => e.ToAddress.Contains(recipient));
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(e => e.Status == status);
        if (from.HasValue) q = q.Where(e => e.At >= from);
        if (to.HasValue) q = q.Where(e => e.At < to);

        var total = await q.CountAsync(ct);
        var clamped = Math.Clamp(limit, 1, 500);

        var rows = await q
            .OrderByDescending(e => e.At)
            .Take(clamped)
            .Select(e => new EmailLogRowDto(
                e.Id, e.At, e.ToAddress, e.Subject, e.Status, e.Error,
                e.TenantId, e.Sender, e.BodyBytes))
            .ToListAsync(ct);

        return new EmailLogPageDto(rows, total);
    }
}
