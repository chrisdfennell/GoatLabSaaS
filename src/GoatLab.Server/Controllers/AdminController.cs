using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

/// <summary>
/// Cross-tenant admin console endpoints. Protected by the SuperAdmin policy
/// (claim-based). Reads bypass the per-tenant EF query filter via
/// ITenantContext.BypassFilter so counts and lists span every tenant. Mutations
/// write an AdminAuditLog row via IAdminAuditLogger.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = SuperAdminPolicy.Name)]
public class AdminController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAdminAuditLogger _audit;

    public const string MaintenanceSettingKey = "maintenance.mode";
    public const string MaintenanceEnabledAtKey = "maintenance.enabled_at";

    public AdminController(
        GoatLabDbContext db,
        ITenantContext tenantContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAdminAuditLogger audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userManager = userManager;
        _signInManager = signInManager;
        _audit = audit;
    }

    // --------- Metrics ---------

    [HttpGet("metrics")]
    public async Task<ActionResult<AdminMetrics>> GetMetrics()
    {
        _tenantContext.BypassFilter = true;
        var now = DateTime.UtcNow;
        var cutoff30 = now.AddDays(-30);
        var cutoff7 = now.AddDays(-7);

        var tenantCount = await _db.Tenants.CountAsync();
        var userCount = await _userManager.Users.CountAsync();
        var goatCount = await _db.Goats.CountAsync();

        var activeTenants = await _db.Goats
            .Where(g => g.CreatedAt >= cutoff30)
            .Select(g => g.TenantId).Distinct().CountAsync();

        var signups7 = await _db.Tenants.CountAsync(t => t.CreatedAt >= cutoff7);
        var signups30 = await _db.Tenants.CountAsync(t => t.CreatedAt >= cutoff30);

        return new AdminMetrics(tenantCount, userCount, goatCount, activeTenants, signups7, signups30);
    }

    [HttpGet("metrics/timeseries")]
    public async Task<ActionResult<AdminTimeseries>> GetTimeseries([FromQuery] int days = 30)
    {
        _tenantContext.BypassFilter = true;
        days = Math.Clamp(days, 7, 365);

        var end = DateTime.UtcNow.Date.AddDays(1);
        var start = end.AddDays(-days);

        var tenantDaily = await _db.Tenants
            .Where(t => t.CreatedAt >= start && t.CreatedAt < end)
            .GroupBy(t => new DateTime(t.CreatedAt.Year, t.CreatedAt.Month, t.CreatedAt.Day))
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Day, x => x.Count);

        var userDaily = await _userManager.Users
            .Where(u => u.CreatedAt >= start && u.CreatedAt < end)
            .GroupBy(u => new DateTime(u.CreatedAt.Year, u.CreatedAt.Month, u.CreatedAt.Day))
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Day, x => x.Count);

        var goatDaily = await _db.Goats
            .Where(g => g.CreatedAt >= start && g.CreatedAt < end)
            .GroupBy(g => new DateTime(g.CreatedAt.Year, g.CreatedAt.Month, g.CreatedAt.Day))
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Day, x => x.Count);

        var points = new List<AdminTimeseriesPoint>(days);
        for (var d = start; d < end; d = d.AddDays(1))
        {
            points.Add(new AdminTimeseriesPoint(
                d,
                tenantDaily.GetValueOrDefault(d, 0),
                userDaily.GetValueOrDefault(d, 0),
                goatDaily.GetValueOrDefault(d, 0)));
        }
        return new AdminTimeseries(points);
    }

    // --------- Tenants ---------

    [HttpGet("tenants")]
    public async Task<ActionResult<List<AdminTenantRow>>> GetTenants()
    {
        _tenantContext.BypassFilter = true;

        var goatCounts = await _db.Goats
            .GroupBy(g => g.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count(), LastAt = g.Max(x => x.CreatedAt) })
            .ToDictionaryAsync(x => x.TenantId);

        var tenants = await _db.Tenants
            .AsNoTracking()
            .Select(t => new
            {
                t.Id, t.Name, t.Slug, t.Location, t.CreatedAt, t.UpdatedAt,
                MemberCount = t.Members.Count,
            })
            .ToListAsync();

        return tenants
            .Select(t =>
            {
                var g = goatCounts.TryGetValue(t.Id, out var gc) ? gc : null;
                DateTime? last = g?.LastAt > t.UpdatedAt ? g?.LastAt : t.UpdatedAt;
                return new AdminTenantRow(t.Id, t.Name, t.Slug, t.Location, t.CreatedAt,
                    t.MemberCount, g?.Count ?? 0, last);
            })
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }

    [HttpGet("tenants/{id}")]
    public async Task<ActionResult<AdminTenantDetail>> GetTenantDetail(int id)
    {
        _tenantContext.BypassFilter = true;
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        var goatCount = await _db.Goats.CountAsync(g => g.TenantId == id);
        var milkCount = await _db.MilkLogs.CountAsync(m => m.TenantId == id);
        var medCount = await _db.MedicalRecords.CountAsync(m => m.TenantId == id);

        var lastGoat = await _db.Goats.Where(g => g.TenantId == id)
            .OrderByDescending(g => g.CreatedAt).Select(g => (DateTime?)g.CreatedAt).FirstOrDefaultAsync();

        var members = await _db.TenantMembers
            .Where(m => m.TenantId == id)
            .Join(_userManager.Users, m => m.UserId, u => u.Id, (m, u) => new { m, u })
            .Select(x => new AdminTenantMemberRow(
                x.u.Id, x.u.Email ?? string.Empty, x.u.DisplayName,
                x.m.Role.ToString(), x.m.JoinedAt))
            .ToListAsync();

        DateTime? lastActivity = lastGoat > tenant.UpdatedAt ? lastGoat : tenant.UpdatedAt;
        var flags = ParseFlags(tenant.FeatureFlagsJson);

        return new AdminTenantDetail(
            tenant.Id, tenant.Name, tenant.Slug, tenant.Location,
            tenant.CreatedAt, tenant.UpdatedAt,
            goatCount, milkCount, medCount, lastActivity, members,
            tenant.SuspendedAt, tenant.SuspensionReason,
            tenant.DeletedAt, tenant.Notes, tenant.Tag, flags);
    }

    [HttpPut("tenants/{id}")]
    public async Task<IActionResult> RenameTenant(int id, AdminRenameTenantRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || req.Name.Length > 100)
            return BadRequest(new { error = "Name required, 1–100 chars." });

        _tenantContext.BypassFilter = true;
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        var oldName = tenant.Name;
        tenant.Name = req.Name.Trim();
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("tenant.rename", "Tenant", id.ToString(), $"{oldName} → {tenant.Name}");
        return NoContent();
    }

    [HttpPost("tenants/{id}/suspend")]
    public async Task<IActionResult> SuspendTenant(int id, AdminSuspendTenantRequest req)
    {
        _tenantContext.BypassFilter = true;
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();
        if (tenant.SuspendedAt is not null) return NoContent();

        tenant.SuspendedAt = DateTime.UtcNow;
        tenant.SuspensionReason = Truncate(req.Reason, 500);
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await InvalidateSessionsForTenantMembers(id);
        await _audit.LogAsync("tenant.suspend", "Tenant", id.ToString(), req.Reason);
        return NoContent();
    }

    [HttpPost("tenants/{id}/unsuspend")]
    public async Task<IActionResult> UnsuspendTenant(int id)
    {
        _tenantContext.BypassFilter = true;
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        tenant.SuspendedAt = null;
        tenant.SuspensionReason = null;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("tenant.unsuspend", "Tenant", id.ToString());
        return NoContent();
    }

    [HttpPost("tenants/{id}/delete")]
    public async Task<IActionResult> DeleteTenant(int id)
    {
        _tenantContext.BypassFilter = true;
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();
        if (tenant.DeletedAt is not null) return NoContent();

        tenant.DeletedAt = DateTime.UtcNow;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await InvalidateSessionsForTenantMembers(id);
        await _audit.LogAsync("tenant.delete", "Tenant", id.ToString(), tenant.Name);
        return NoContent();
    }

    [HttpPost("tenants/{id}/restore")]
    public async Task<IActionResult> RestoreTenant(int id)
    {
        _tenantContext.BypassFilter = true;
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        tenant.DeletedAt = null;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("tenant.restore", "Tenant", id.ToString(), tenant.Name);
        return NoContent();
    }

    [HttpPut("tenants/{id}/notes")]
    public async Task<IActionResult> SetNotes(int id, AdminTenantNotesRequest req)
    {
        _tenantContext.BypassFilter = true;
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        tenant.Notes = Truncate(req.Notes, 4000);
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("tenant.notes", "Tenant", id.ToString());
        return NoContent();
    }

    [HttpPut("tenants/{id}/tag")]
    public async Task<IActionResult> SetTag(int id, AdminTenantTagRequest req)
    {
        _tenantContext.BypassFilter = true;
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        var newTag = string.IsNullOrWhiteSpace(req.Tag) ? null : Truncate(req.Tag.Trim(), 50);
        var old = tenant.Tag;
        tenant.Tag = newTag;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("tenant.tag", "Tenant", id.ToString(), $"{old ?? "—"} → {newTag ?? "—"}");
        return NoContent();
    }

    [HttpPut("tenants/{id}/flags")]
    public async Task<IActionResult> SetFlags(int id, AdminTenantFlagsRequest req)
    {
        _tenantContext.BypassFilter = true;
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        var known = TenantFeatureFlags.All.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        var filtered = req.Flags.Where(kv => known.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        tenant.FeatureFlagsJson = filtered.Count == 0 ? null : JsonSerializer.Serialize(filtered);
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("tenant.flags", "Tenant", id.ToString(),
            string.Join(", ", filtered.Select(kv => $"{kv.Key}={kv.Value}")));
        return NoContent();
    }

    // --------- Users ---------

    [HttpGet("users")]
    public async Task<ActionResult<List<AdminUserRow>>> GetUsers()
    {
        _tenantContext.BypassFilter = true;

        var memCounts = await _db.TenantMembers
            .GroupBy(m => m.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        var users = await _userManager.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        return users.Select(u => new AdminUserRow(
            u.Id, u.Email ?? string.Empty, u.DisplayName, u.CreatedAt,
            u.IsSuperAdmin, memCounts.GetValueOrDefault(u.Id, 0))).ToList();
    }

    [HttpGet("users/{id}")]
    public async Task<ActionResult<AdminUserDetail>> GetUserDetail(string id)
    {
        _tenantContext.BypassFilter = true;
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var memberships = await _db.TenantMembers
            .Where(m => m.UserId == id)
            .Join(_db.Tenants.IgnoreQueryFilters(), m => m.TenantId, t => t.Id,
                (m, t) => new AdminUserMembershipRow(
                    t.Id, t.Name, t.Slug, m.Role.ToString(), m.JoinedAt))
            .ToListAsync();

        return new AdminUserDetail(
            user.Id, user.Email ?? string.Empty, user.DisplayName,
            user.CreatedAt, user.IsSuperAdmin,
            user.LockoutEnabled, user.LockoutEnd, user.DeletedAt, memberships);
    }

    [HttpPost("users/{id}/super-admin")]
    public async Task<IActionResult> ToggleSuperAdmin(string id, AdminToggleSuperAdminRequest req)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (callerId == id && !req.IsSuperAdmin)
            return BadRequest(new { error = "You can't remove your own super-admin role." });

        if (user.IsSuperAdmin == req.IsSuperAdmin) return NoContent();
        user.IsSuperAdmin = req.IsSuperAdmin;
        await _userManager.UpdateAsync(user);

        await _audit.LogAsync(
            req.IsSuperAdmin ? "user.grant_super_admin" : "user.revoke_super_admin",
            "User", id, user.Email);
        return NoContent();
    }

    [HttpPost("users/{id}/reset-password")]
    public async Task<ActionResult<AdminResetPasswordResponse>> ResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var temp = GenerateTempPassword();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, temp);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await _audit.LogAsync("user.reset_password", "User", id, user.Email);
        return new AdminResetPasswordResponse(temp);
    }

    [HttpPost("users/{id}/lock")]
    public async Task<IActionResult> LockUser(string id, AdminLockUserRequest req)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (callerId == id) return BadRequest(new { error = "You can't lock yourself out." });

        // Default to a far-future date for an indefinite lock; otherwise limited duration.
        var end = req.DurationHours is int h && h > 0
            ? DateTimeOffset.UtcNow.AddHours(h)
            : DateTimeOffset.UtcNow.AddYears(100);

        if (!user.LockoutEnabled) user.LockoutEnabled = true;
        user.LockoutEnd = end;
        await _userManager.UpdateAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);

        await _audit.LogAsync("user.lock", "User", id,
            req.DurationHours is null ? "indefinite" : $"{req.DurationHours}h");
        return NoContent();
    }

    [HttpPost("users/{id}/unlock")]
    public async Task<IActionResult> UnlockUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        user.LockoutEnd = null;
        await _userManager.UpdateAsync(user);
        await _userManager.ResetAccessFailedCountAsync(user);

        await _audit.LogAsync("user.unlock", "User", id);
        return NoContent();
    }

    [HttpPost("users/{id}/sign-out-everywhere")]
    public async Task<IActionResult> ForceSignOut(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        // Rotating the security stamp invalidates any existing auth cookie on
        // next SecurityStampValidator run (default 30 min — fine for support
        // purposes; if we need faster, shorten ValidationInterval).
        await _userManager.UpdateSecurityStampAsync(user);

        await _audit.LogAsync("user.force_signout", "User", id);
        return NoContent();
    }

    [HttpPost("users/{id}/delete")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (callerId == id) return BadRequest(new { error = "You can't delete yourself." });
        if (user.DeletedAt is not null) return NoContent();

        user.DeletedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);

        await _audit.LogAsync("user.delete", "User", id, user.Email);
        return NoContent();
    }

    [HttpPost("users/{id}/restore")]
    public async Task<IActionResult> RestoreUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        user.DeletedAt = null;
        await _userManager.UpdateAsync(user);

        await _audit.LogAsync("user.restore", "User", id, user.Email);
        return NoContent();
    }

    // --------- Impersonation ---------

    public const string OriginalTenantClaimType = "original_tenant_id";

    [HttpGet("impersonation")]
    public async Task<ActionResult<ImpersonationState?>> GetImpersonation()
    {
        var origClaim = User.FindFirstValue(OriginalTenantClaimType);
        if (!int.TryParse(origClaim, out var origId)) return Ok(null as ImpersonationState);

        _tenantContext.BypassFilter = true;
        var currentId = int.TryParse(User.FindFirstValue(TenantContextMiddleware.TenantClaimType), out var c) ? c : 0;
        var current = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == currentId);
        var original = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == origId);
        if (current is null || original is null) return Ok(null as ImpersonationState);

        return Ok(new ImpersonationState(current.Id, current.Name, original.Id, original.Name));
    }

    [HttpPost("impersonate/{tenantId}")]
    public async Task<IActionResult> StartImpersonation(int tenantId)
    {
        _tenantContext.BypassFilter = true;
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant is null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var originalClaim = User.FindFirstValue(OriginalTenantClaimType)
            ?? User.FindFirstValue(TenantContextMiddleware.TenantClaimType);

        var claims = new List<Claim>
        {
            new(TenantContextMiddleware.TenantClaimType, tenantId.ToString()),
            new(SuperAdminPolicy.ClaimType, "true"),
        };
        if (!string.IsNullOrEmpty(originalClaim))
            claims.Add(new Claim(OriginalTenantClaimType, originalClaim));

        await _signInManager.SignInWithClaimsAsync(user, isPersistent: true, claims);
        await _audit.LogAsync("impersonation.start", "Tenant", tenantId.ToString(), tenant.Name);
        return NoContent();
    }

    [HttpPost("impersonate/exit")]
    public async Task<IActionResult> ExitImpersonation()
    {
        var origClaim = User.FindFirstValue(OriginalTenantClaimType);
        if (!int.TryParse(origClaim, out var origId))
            return BadRequest(new { error = "Not currently impersonating." });

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var claims = new List<Claim>
        {
            new(TenantContextMiddleware.TenantClaimType, origId.ToString()),
            new(SuperAdminPolicy.ClaimType, "true"),
        };
        await _signInManager.SignInWithClaimsAsync(user, isPersistent: true, claims);
        await _audit.LogAsync("impersonation.exit", "Tenant", origId.ToString());
        return NoContent();
    }

    // --------- Audit log ---------

    [HttpGet("audit")]
    public async Task<ActionResult<AdminAuditPage>> GetAudit(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        _tenantContext.BypassFilter = true;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 200);

        var q = _db.AdminAuditLogs.OrderByDescending(a => a.At);
        var total = await q.CountAsync();

        var rows = await q
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AdminAuditRow(
                a.Id, a.At, a.ActorEmail, a.Action,
                a.TargetType, a.TargetId, a.Detail))
            .ToListAsync();

        return new AdminAuditPage(rows, page, pageSize, total);
    }

    [HttpGet("audit/export")]
    public async Task<IActionResult> ExportAudit()
    {
        _tenantContext.BypassFilter = true;
        var all = await _db.AdminAuditLogs.OrderByDescending(a => a.At).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("At,Actor,Action,TargetType,TargetId,Detail");
        foreach (var a in all)
        {
            sb.Append(a.At.ToString("o", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Csv(a.ActorEmail)).Append(',');
            sb.Append(Csv(a.Action)).Append(',');
            sb.Append(Csv(a.TargetType ?? "")).Append(',');
            sb.Append(Csv(a.TargetId ?? "")).Append(',');
            sb.AppendLine(Csv(a.Detail ?? ""));
        }
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv",
            $"goatlab-audit-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }

    // --------- Announcements ---------

    [HttpGet("announcements")]
    public async Task<ActionResult<List<AdminAnnouncementRow>>> GetAnnouncements()
    {
        var list = await _db.Announcements
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AdminAnnouncementRow(
                a.Id, a.Title, a.Body, a.Severity.ToString(), a.TargetTag,
                a.StartsAt, a.EndsAt, a.IsActive, a.CreatedAt))
            .ToListAsync();
        return list;
    }

    [HttpPost("announcements")]
    public async Task<ActionResult<AdminAnnouncementRow>> CreateAnnouncement(AdminAnnouncementUpsert req)
    {
        if (!Enum.TryParse<AnnouncementSeverity>(req.Severity, ignoreCase: true, out var sev))
            return BadRequest(new { error = "Severity must be Info, Warning, or Critical." });
        if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Body))
            return BadRequest(new { error = "Title and body required." });

        var a = new Announcement
        {
            Title = req.Title.Trim(),
            Body = req.Body.Trim(),
            Severity = sev,
            TargetTag = string.IsNullOrWhiteSpace(req.TargetTag) ? null : req.TargetTag.Trim(),
            StartsAt = req.StartsAt,
            EndsAt = req.EndsAt,
            IsActive = req.IsActive,
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
        };
        _db.Announcements.Add(a);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("announcement.create", "Announcement", a.Id.ToString(), a.Title);
        return new AdminAnnouncementRow(a.Id, a.Title, a.Body, a.Severity.ToString(),
            a.TargetTag, a.StartsAt, a.EndsAt, a.IsActive, a.CreatedAt);
    }

    [HttpPut("announcements/{id}")]
    public async Task<IActionResult> UpdateAnnouncement(int id, AdminAnnouncementUpsert req)
    {
        var a = await _db.Announcements.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        if (!Enum.TryParse<AnnouncementSeverity>(req.Severity, ignoreCase: true, out var sev))
            return BadRequest(new { error = "Severity must be Info, Warning, or Critical." });

        a.Title = req.Title.Trim();
        a.Body = req.Body.Trim();
        a.Severity = sev;
        a.TargetTag = string.IsNullOrWhiteSpace(req.TargetTag) ? null : req.TargetTag.Trim();
        a.StartsAt = req.StartsAt;
        a.EndsAt = req.EndsAt;
        a.IsActive = req.IsActive;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("announcement.update", "Announcement", id.ToString(), a.Title);
        return NoContent();
    }

    [HttpDelete("announcements/{id}")]
    public async Task<IActionResult> DeleteAnnouncement(int id)
    {
        var a = await _db.Announcements.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        _db.Announcements.Remove(a);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("announcement.delete", "Announcement", id.ToString(), a.Title);
        return NoContent();
    }

    // --------- Maintenance mode ---------

    [HttpGet("maintenance")]
    public async Task<ActionResult<AdminMaintenanceStatus>> GetMaintenance()
    {
        var enabled = await IsMaintenanceEnabledAsync(_db);
        var at = await _db.AppSettings.Where(s => s.Key == MaintenanceEnabledAtKey)
            .Select(s => (string?)s.Value).FirstOrDefaultAsync();
        DateTime? parsed = DateTime.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var p)
            ? DateTime.SpecifyKind(p, DateTimeKind.Utc) : null;
        return new AdminMaintenanceStatus(enabled, parsed);
    }

    [HttpPost("maintenance")]
    public async Task<IActionResult> SetMaintenance(AdminMaintenanceRequest req)
    {
        await UpsertSettingAsync(_db, MaintenanceSettingKey, req.Enabled ? "true" : "false");
        await UpsertSettingAsync(_db, MaintenanceEnabledAtKey,
            req.Enabled ? DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) : "");

        await _audit.LogAsync(req.Enabled ? "maintenance.on" : "maintenance.off", "System");
        return NoContent();
    }

    public static async Task<bool> IsMaintenanceEnabledAsync(GoatLabDbContext db)
    {
        var v = await db.AppSettings.Where(s => s.Key == MaintenanceSettingKey)
            .Select(s => s.Value).FirstOrDefaultAsync();
        return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task UpsertSettingAsync(GoatLabDbContext db, string key, string value)
    {
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (row is null) db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        else row.Value = value;
        await db.SaveChangesAsync();
    }

    // --------- Helpers ---------

    private async Task InvalidateSessionsForTenantMembers(int tenantId)
    {
        var userIds = await _db.TenantMembers
            .Where(m => m.TenantId == tenantId)
            .Select(m => m.UserId)
            .ToListAsync();

        foreach (var uid in userIds)
        {
            var u = await _userManager.FindByIdAsync(uid);
            if (u is not null) await _userManager.UpdateSecurityStampAsync(u);
        }
    }

    private static IReadOnlyDictionary<string, bool> ParseFlags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, bool>();
        try { return JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new(); }
        catch { return new Dictionary<string, bool>(); }
    }

    private static string? Truncate(string? s, int max) =>
        s is null ? null : s.Length <= max ? s : s[..max];

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static string GenerateTempPassword()
    {
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string upper = "ABCDEFGHJKMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string symbols = "!@#$%&*";
        var all = lower + upper + digits + symbols;
        var rng = Random.Shared;
        Span<char> buf = stackalloc char[12];
        buf[0] = lower[rng.Next(lower.Length)];
        buf[1] = upper[rng.Next(upper.Length)];
        buf[2] = digits[rng.Next(digits.Length)];
        buf[3] = symbols[rng.Next(symbols.Length)];
        for (int i = 4; i < buf.Length; i++) buf[i] = all[rng.Next(all.Length)];
        for (int i = buf.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (buf[i], buf[j]) = (buf[j], buf[i]);
        }
        return new string(buf);
    }
}
