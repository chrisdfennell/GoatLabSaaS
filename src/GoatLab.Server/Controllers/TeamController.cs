using System.Security.Cryptography;
using System.Text;
using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services;
using GoatLab.Server.Services.Email;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Tenant-owner self-service for team management: invite, list, remove, change
// role, accept invite. MaxUsers cap is enforced at invite-creation time (not
// accept), so no race between owner clicking "Send" and all seats being taken.
[ApiController]
[Route("api/team")]
public class TeamController : ControllerBase
{
    private const int InviteExpiryDays = 7;

    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppEmailSender _email;
    private readonly IFeatureGate _featureGate;

    public TeamController(
        GoatLabDbContext db,
        ITenantContext tenantContext,
        UserManager<ApplicationUser> userManager,
        IAppEmailSender email,
        IFeatureGate featureGate)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userManager = userManager;
        _email = email;
        _featureGate = featureGate;
    }

    public record MemberDto(string UserId, string Email, string DisplayName, TenantRole Role, DateTime JoinedAt);
    public record InviteDto(int Id, string Email, TenantRole Role, DateTime CreatedAt, DateTime ExpiresAt);
    public record TeamDto(List<MemberDto> Members, List<InviteDto> PendingInvites, int? MaxUsers);

    public record CreateInviteRequest(string Email, TenantRole Role);
    public record ChangeRoleRequest(TenantRole Role);
    public record AcceptInviteRequest(string Token);

    [HttpGet]
    public async Task<ActionResult<TeamDto>> Get(CancellationToken ct)
    {
        if (_tenantContext.TenantId is not int tenantId) return NotFound();

        _tenantContext.BypassFilter = true;
        List<MemberDto> members;
        try
        {
            // Order on entity fields before projection — SQL Server EF provider
            // can't translate ORDER BY against a projected record's property.
            members = await _db.TenantMembers
                .Where(m => m.TenantId == tenantId)
                .OrderBy(m => m.JoinedAt)
                .Join(_db.Users, m => m.UserId, u => u.Id, (m, u) =>
                    new MemberDto(u.Id, u.Email ?? "", u.DisplayName, m.Role, m.JoinedAt))
                .ToListAsync(ct);
        }
        finally { _tenantContext.BypassFilter = false; }

        var invites = await _db.TenantInvitations
            .Where(i => i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InviteDto(i.Id, i.Email, i.Role, i.CreatedAt, i.ExpiresAt))
            .ToListAsync(ct);

        var plan = await _featureGate.GetCurrentPlanAsync(ct);
        return new TeamDto(members, invites, plan?.MaxUsers);
    }

    [HttpPost("invites")]
    public async Task<ActionResult<InviteDto>> CreateInvite([FromBody] CreateInviteRequest req, CancellationToken ct)
    {
        if (_tenantContext.TenantId is not int tenantId) return NotFound();
        if (!await IsOwnerAsync(tenantId, ct)) return Forbid();
        if (req.Role == TenantRole.Owner) return BadRequest(new { error = "Only existing owners can promote others. Start with Manager/Worker/ReadOnly." });

        var email = (req.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            return BadRequest(new { error = "Valid email required." });

        // Seat cap — counts existing members + outstanding invites to match what
        // the owner would see if the invite were accepted right now.
        _tenantContext.BypassFilter = true;
        int memberCount;
        bool existingMember;
        try
        {
            memberCount = await _db.TenantMembers.CountAsync(m => m.TenantId == tenantId, ct);
            existingMember = await _db.TenantMembers
                .AnyAsync(m => m.TenantId == tenantId
                            && _db.Users.Any(u => u.Id == m.UserId && u.NormalizedEmail == email.ToUpperInvariant()), ct);
        }
        finally { _tenantContext.BypassFilter = false; }

        if (existingMember)
            return Conflict(new { error = "That email is already on the team." });

        var outstandingInvites = await _db.TenantInvitations.CountAsync(
            i => i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > DateTime.UtcNow, ct);

        var plan = await _featureGate.GetCurrentPlanAsync(ct);
        if (plan?.MaxUsers is int cap && memberCount + outstandingInvites >= cap)
        {
            return new ObjectResult(new
            {
                error = $"Your plan allows {cap} user{(cap == 1 ? "" : "s")}. Upgrade or revoke an invite to add more.",
                upgradeRequired = true,
                limit = "MaxUsers",
            })
            { StatusCode = StatusCodes.Status402PaymentRequired };
        }

        // Dedupe pending invites for the same email.
        var existingInvite = await _db.TenantInvitations.FirstOrDefaultAsync(
            i => i.Email == email && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > DateTime.UtcNow, ct);
        if (existingInvite is not null)
            return Conflict(new { error = "An active invite for that email already exists.", inviteId = existingInvite.Id });

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Unauthorized();

        var rawToken = GenerateToken();
        var invite = new TenantInvitation
        {
            TenantId = tenantId,
            Email = email,
            Role = req.Role,
            TokenHash = Sha256(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(InviteExpiryDays),
            CreatedByUserId = currentUser.Id,
        };
        _db.TenantInvitations.Add(invite);
        await _db.SaveChangesAsync(ct);

        // Fire-and-forget email; failure doesn't unwind the invite (owner can resend).
        try
        {
            var tenant = await _db.Tenants.FirstAsync(t => t.Id == tenantId, ct);
            var url = $"{Request.Scheme}://{Request.Host}/accept-invite?token={Uri.EscapeDataString(rawToken)}";
            var tpl = EmailTemplates.TeamInvitation(currentUser.DisplayName, tenant.Name, invite.Role.ToString(), url);
            await _email.SendAsync(email, tpl.Subject, tpl.Html, tpl.Text, ct);
        }
        catch { /* logged by email sender; response still reflects invite existence */ }

        return new InviteDto(invite.Id, invite.Email, invite.Role, invite.CreatedAt, invite.ExpiresAt);
    }

    [HttpDelete("invites/{id:int}")]
    public async Task<IActionResult> RevokeInvite(int id, CancellationToken ct)
    {
        if (_tenantContext.TenantId is not int tenantId) return NotFound();
        if (!await IsOwnerAsync(tenantId, ct)) return Forbid();

        var invite = await _db.TenantInvitations.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invite is null) return NotFound();
        invite.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // Accept is deliberately NOT tenant-scoped — the caller is joining a new
    // tenant, not acting within an existing one. We hash the provided token and
    // use it as the lookup key.
    [HttpPost("invites/accept")]
    public async Task<IActionResult> Accept([FromBody] AcceptInviteRequest req, CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var hash = Sha256(req.Token ?? string.Empty);

        _tenantContext.BypassFilter = true;
        TenantInvitation? invite;
        try
        {
            invite = await _db.TenantInvitations.FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
            if (invite is null) return NotFound(new { error = "Invite not found." });
            if (invite.AcceptedAt is not null) return BadRequest(new { error = "Invite was already accepted." });
            if (invite.RevokedAt is not null) return BadRequest(new { error = "Invite was revoked." });
            if (invite.ExpiresAt < DateTime.UtcNow) return BadRequest(new { error = "Invite has expired." });

            if (!string.Equals(user.Email ?? "", invite.Email, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = $"This invite was sent to {invite.Email}. Sign in with that account to accept." });

            var alreadyMember = await _db.TenantMembers
                .AnyAsync(m => m.TenantId == invite.TenantId && m.UserId == user.Id, ct);
            if (!alreadyMember)
            {
                _db.TenantMembers.Add(new TenantMember
                {
                    TenantId = invite.TenantId,
                    UserId = user.Id,
                    Role = invite.Role,
                });
            }

            invite.AcceptedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        finally { _tenantContext.BypassFilter = false; }

        return Ok(new { tenantId = invite.TenantId });
    }

    [HttpDelete("members/{userId}")]
    public async Task<IActionResult> RemoveMember(string userId, CancellationToken ct)
    {
        if (_tenantContext.TenantId is not int tenantId) return NotFound();
        if (!await IsOwnerAsync(tenantId, ct)) return Forbid();

        _tenantContext.BypassFilter = true;
        try
        {
            var target = await _db.TenantMembers.FirstOrDefaultAsync(
                m => m.TenantId == tenantId && m.UserId == userId, ct);
            if (target is null) return NotFound();

            if (target.Role == TenantRole.Owner)
            {
                var ownerCount = await _db.TenantMembers.CountAsync(
                    m => m.TenantId == tenantId && m.Role == TenantRole.Owner, ct);
                if (ownerCount <= 1)
                    return BadRequest(new { error = "Can't remove the last owner. Transfer ownership first." });
            }

            _db.TenantMembers.Remove(target);
            await _db.SaveChangesAsync(ct);
        }
        finally { _tenantContext.BypassFilter = false; }

        return NoContent();
    }

    [HttpPut("members/{userId}/role")]
    public async Task<IActionResult> ChangeRole(string userId, [FromBody] ChangeRoleRequest req, CancellationToken ct)
    {
        if (_tenantContext.TenantId is not int tenantId) return NotFound();
        if (!await IsOwnerAsync(tenantId, ct)) return Forbid();

        _tenantContext.BypassFilter = true;
        try
        {
            var target = await _db.TenantMembers.FirstOrDefaultAsync(
                m => m.TenantId == tenantId && m.UserId == userId, ct);
            if (target is null) return NotFound();

            // Prevent demoting the last Owner.
            if (target.Role == TenantRole.Owner && req.Role != TenantRole.Owner)
            {
                var ownerCount = await _db.TenantMembers.CountAsync(
                    m => m.TenantId == tenantId && m.Role == TenantRole.Owner, ct);
                if (ownerCount <= 1)
                    return BadRequest(new { error = "Can't demote the last owner. Promote another member first." });
            }

            target.Role = req.Role;
            await _db.SaveChangesAsync(ct);
        }
        finally { _tenantContext.BypassFilter = false; }

        return NoContent();
    }

    private async Task<bool> IsOwnerAsync(int tenantId, CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return false;

        _tenantContext.BypassFilter = true;
        try
        {
            return await _db.TenantMembers.AnyAsync(
                m => m.TenantId == tenantId && m.UserId == user.Id && m.Role == TenantRole.Owner, ct);
        }
        finally { _tenantContext.BypassFilter = false; }
    }

    private static string GenerateToken()
    {
        // 32 bytes of randomness → 43-char URL-safe Base64 (plenty of entropy).
        var bytes = RandomNumberGenerator.GetBytes(32);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    private static string Sha256(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
