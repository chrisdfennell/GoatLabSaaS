using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services;
using GoatLab.Server.Services.Email;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAppEmailSender _email;
    private readonly IdentityOptions _identityOptions;
    private readonly IFido2 _fido2;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        GoatLabDbContext db,
        ITenantContext tenantContext,
        IAppEmailSender email,
        IOptions<IdentityOptions> identityOptions,
        IFido2 fido2,
        IMemoryCache cache,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _tenantContext = tenantContext;
        _email = email;
        _identityOptions = identityOptions.Value;
        _fido2 = fido2;
        _cache = cache;
        _logger = logger;
    }

    private bool RequiresConfirmedEmail => _identityOptions.SignIn.RequireConfirmedEmail;

    [AllowAnonymous]
    [EnableRateLimiting("register")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            DisplayName = req.DisplayName,
        };

        var createResult = await _userManager.CreateAsync(user, req.Password);
        if (!createResult.Succeeded)
            return BadRequest(new { errors = createResult.Errors.Select(e => e.Description) });

        // Bypass the tenant query filter while creating the user's first tenant.
        _tenantContext.BypassFilter = true;

        var slug = Slugify(req.FarmName);
        var baseSlug = slug;
        var n = 1;
        while (await _db.Tenants.AnyAsync(t => t.Slug == slug))
        {
            slug = $"{baseSlug}-{++n}";
        }

        // Assign a default plan (first active public plan, or the Homestead seed).
        var defaultPlanId = await _db.Plans
            .Where(p => p.IsActive && p.IsPublic)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync() ?? 1;

        var tenant = new Tenant { Name = req.FarmName, Slug = slug, PlanId = defaultPlanId };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        _db.TenantMembers.Add(new TenantMember
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = TenantRole.Owner,
        });
        await _db.SaveChangesAsync();

        // Fire off confirmation email. If SMTP isn't configured, NullEmailSender
        // logs + drops the send. Any exception (mail server down, bad creds) is
        // swallowed so signup isn't blocked by transient email failure.
        try { await SendConfirmationEmailAsync(user); }
        catch { /* surfaced via /api/account/resend-confirmation if user notices */ }

        // If confirmation is required, don't auto sign-in — the user must
        // confirm first. Otherwise sign them in and return the full session DTO.
        if (RequiresConfirmedEmail)
        {
            return Ok(new
            {
                requiresConfirmation = true,
                email = user.Email,
                message = "Check your email to confirm your account before signing in.",
            });
        }

        await _signInManager.SignInWithClaimsAsync(user, isPersistent: true, BuildClaims(user, tenant.Id));
        return Ok(await BuildCurrentUserDto(user));
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user is null) return Unauthorized(new { error = "Invalid email or password." });

        if (user.DeletedAt is not null)
            return Unauthorized(new { error = "Account is disabled. Contact support." });

        // PasswordSignInAsync validates password + lockout + confirmed-email,
        // and — critically — sets the TwoFactorUserIdScheme cookie when 2FA is on.
        var result = await _signInManager.PasswordSignInAsync(user, req.Password, req.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return Unauthorized(new { error = "Account is locked. Try again later or contact support." });
        if (result.IsNotAllowed)
        {
            if (RequiresConfirmedEmail && !await _userManager.IsEmailConfirmedAsync(user))
            {
                return Unauthorized(new
                {
                    error = "Confirm your email before signing in. Check your inbox for the confirmation link.",
                    emailUnconfirmed = true,
                    email = user.Email,
                });
            }
            return Unauthorized(new { error = "Sign-in is not allowed." });
        }

        if (result.RequiresTwoFactor)
        {
            var passkeys = await _db.UserCredentials
                .Where(c => c.UserId == user.Id)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();
            return Ok(new { requiresTwoFactor = true, passkeys });
        }

        if (!result.Succeeded)
            return Unauthorized(new { error = "Invalid email or password." });

        // PasswordSignInAsync signed us in, but without tenant claims. Re-sign
        // to stamp the tenant_id and super_admin claims.
        return await FinalizeSignInAsync(user, req.RememberMe);
    }

    public record VerifyTotpRequest(string Code, bool RememberMe, bool RememberMachine);

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login/verify-totp")]
    public async Task<IActionResult> VerifyTotp([FromBody] VerifyTotpRequest req)
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return Unauthorized(new { error = "Sign-in session expired. Start over." });

        var code = (req.Code ?? "").Replace(" ", "").Replace("-", "");
        var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(code, req.RememberMe, req.RememberMachine);
        if (!result.Succeeded)
        {
            var recResult = await _signInManager.TwoFactorRecoveryCodeSignInAsync(code);
            if (!recResult.Succeeded)
                return Unauthorized(new { error = "Invalid code." });
        }

        return await FinalizeSignInAsync(user, req.RememberMe);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login/passkey-start")]
    public async Task<IActionResult> PasskeyStart()
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return Unauthorized(new { error = "Sign-in session expired." });

        var creds = await _db.UserCredentials
            .Where(c => c.UserId == user.Id)
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToListAsync();
        if (creds.Count == 0) return BadRequest(new { error = "No passkeys registered." });

        var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = creds,
            UserVerification = UserVerificationRequirement.Preferred,
        });
        _cache.Set($"webauthn:login:{user.Id}", options.ToJson(), TimeSpan.FromMinutes(5));
        return Ok(options);
    }

    public record PasskeyLoginRequest(AuthenticatorAssertionRawResponse Response, bool RememberMe);

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login/passkey-complete")]
    public async Task<IActionResult> PasskeyComplete([FromBody] PasskeyLoginRequest req)
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return Unauthorized(new { error = "Sign-in session expired." });

        if (!_cache.TryGetValue<string>($"webauthn:login:{user.Id}", out var json))
            return BadRequest(new { error = "Login challenge expired." });
        var options = AssertionOptions.FromJson(json!);

        // Byte-array comparison in LINQ requires loading candidates and
        // checking in memory. User credential count is tiny so this is fine.
        var responseCredId = WebEncoders.Base64UrlDecode(req.Response.Id);
        var userCreds = await _db.UserCredentials.Where(c => c.UserId == user.Id).ToListAsync();
        var cred = userCreds.FirstOrDefault(c => c.CredentialId.AsSpan().SequenceEqual(responseCredId));
        if (cred is null)
            return BadRequest(new { error = "Credential not recognized." });

        var verified = await _fido2.MakeAssertionAsync(new MakeAssertionParams
        {
            AssertionResponse = req.Response,
            OriginalOptions = options,
            StoredPublicKey = cred.PublicKey,
            StoredSignatureCounter = cred.SignCount,
            IsUserHandleOwnerOfCredentialIdCallback = (_, _) => Task.FromResult(true),
        });

        cred.SignCount = verified.SignCount;
        cred.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _cache.Remove($"webauthn:login:{user.Id}");

        await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
        return await FinalizeSignInAsync(user, req.RememberMe);
    }

    // Shared finalizer: pick a tenant, stamp claims, return full DTO.
    private async Task<IActionResult> FinalizeSignInAsync(ApplicationUser user, bool persist)
    {
        _tenantContext.BypassFilter = true;
        var memberships = await _db.TenantMembers
            .Where(m => m.UserId == user.Id)
            .Include(m => m.Tenant)
            .Where(m => m.Tenant!.DeletedAt == null && m.Tenant!.SuspendedAt == null)
            .ToListAsync();
        int? tenantId = memberships.Count == 1 ? memberships[0].TenantId : (int?)null;

        await _signInManager.SignInWithClaimsAsync(user, persist, BuildClaims(user, tenantId));
        return Ok(await BuildCurrentUserDto(user));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return NoContent();
    }

    [Authorize]
    [HttpPost("select-tenant")]
    public async Task<IActionResult> SelectTenant(SelectTenantRequest req)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        _tenantContext.BypassFilter = true;
        var membership = await _db.TenantMembers
            .Include(m => m.Tenant)
            .FirstOrDefaultAsync(m => m.UserId == user.Id && m.TenantId == req.TenantId);
        if (membership is null) return Forbid();
        if (membership.Tenant?.DeletedAt is not null)
            return BadRequest(new { error = "This farm has been deleted." });
        if (membership.Tenant?.SuspendedAt is not null)
            return BadRequest(new { error = "This farm is suspended. Contact support." });

        await _signInManager.SignInWithClaimsAsync(user, isPersistent: true, BuildClaims(user, req.TenantId));
        return Ok(await BuildCurrentUserDto(user));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        return Ok(await BuildCurrentUserDto(user));
    }

    /// <summary>
    /// Diagnostic — dumps the request's authenticated identities + every claim
    /// they carry. Cheap way to confirm whether <c>super_admin</c> is actually
    /// on the cookie when an admin endpoint unexpectedly returns 403. Cookie
    /// auth only (api keys can't authorize themselves into a debug endpoint).
    /// </summary>
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    [HttpGet("whoami")]
    public IActionResult WhoAmI() => Ok(new
    {
        IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
        AuthType = User.Identity?.AuthenticationType,
        Identities = User.Identities.Select(i => new
        {
            i.AuthenticationType,
            i.IsAuthenticated,
            Claims = i.Claims.Select(c => new { c.Type, c.Value }).ToArray()
        }).ToArray()
    });

    // GDPR-style data export. Returns a zip of the user's profile, memberships,
    // and — for tenants they own — the main farm records. Scoped to owned
    // tenants on purpose: a shared tenant's data belongs to the owner, not to
    // every member.
    [Authorize]
    [HttpGet("export-data")]
    public async Task<IActionResult> ExportData(CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        _tenantContext.BypassFilter = true;

        var memberships = await _db.TenantMembers
            .Include(m => m.Tenant)
            .Where(m => m.UserId == user.Id)
            .ToListAsync(ct);

        var ownedTenantIds = memberships
            .Where(m => m.Role == TenantRole.Owner && m.Tenant is not null)
            .Select(m => m.TenantId)
            .ToList();

        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteJsonEntryAsync(zip, "profile.json", new
            {
                user.Id, user.Email, user.UserName, user.DisplayName, user.CreatedAt, user.IsSuperAdmin
            }, opts, ct);

            await WriteJsonEntryAsync(zip, "memberships.json", memberships.Select(m => new
            {
                m.TenantId,
                TenantName = m.Tenant?.Name,
                TenantSlug = m.Tenant?.Slug,
                m.Role,
                m.JoinedAt,
            }), opts, ct);

            foreach (var tenantId in ownedTenantIds)
            {
                var folder = $"tenant-{tenantId}/";
                await WriteJsonEntryAsync(zip, folder + "tenant.json",
                    await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct), opts, ct);
                await WriteJsonEntryAsync(zip, folder + "goats.json",
                    await _db.Goats.Where(g => g.TenantId == tenantId).ToListAsync(ct), opts, ct);
                await WriteJsonEntryAsync(zip, folder + "medical-records.json",
                    await _db.MedicalRecords.Where(r => r.TenantId == tenantId).ToListAsync(ct), opts, ct);
                await WriteJsonEntryAsync(zip, folder + "milk-logs.json",
                    await _db.MilkLogs.Where(r => r.TenantId == tenantId).ToListAsync(ct), opts, ct);
                await WriteJsonEntryAsync(zip, folder + "breeding-records.json",
                    await _db.BreedingRecords.Where(r => r.TenantId == tenantId).ToListAsync(ct), opts, ct);
                await WriteJsonEntryAsync(zip, folder + "sales.json",
                    await _db.Sales.Where(r => r.TenantId == tenantId).ToListAsync(ct), opts, ct);
                await WriteJsonEntryAsync(zip, folder + "transactions.json",
                    await _db.Transactions.Where(r => r.TenantId == tenantId).ToListAsync(ct), opts, ct);
            }

            var readme = "GoatLab data export\n"
                + $"Generated: {DateTime.UtcNow:O}\n"
                + $"User: {user.Email}\n\n"
                + "profile.json — your account profile\n"
                + "memberships.json — tenants you belong to and your role\n"
                + "tenant-<id>/ — full records for each tenant you own\n";
            var readmeEntry = zip.CreateEntry("README.txt", CompressionLevel.Optimal);
            await using var rw = new StreamWriter(readmeEntry.Open(), Encoding.UTF8);
            await rw.WriteAsync(readme);
        }

        ms.Position = 0;
        var fileName = $"goatlab-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
        return File(ms.ToArray(), "application/zip", fileName);
    }

    private static async Task WriteJsonEntryAsync(ZipArchive zip, string path, object? data, JsonSerializerOptions opts, CancellationToken ct)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, data, opts, ct);
    }

    // Soft-deletes the user. Leaves tenants alone: a shared tenant survives so
    // the remaining members don't lose their farm. If the user is the sole
    // owner of a tenant, the tenant is soft-deleted too. A 30-day hard-delete
    // sweep is not yet implemented — it needs a background job runner.
    [Authorize]
    [HttpPost("delete-my-account")]
    public async Task<IActionResult> DeleteMyAccount(CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        _tenantContext.BypassFilter = true;

        var now = DateTime.UtcNow;
        user.DeletedAt = now;

        var memberships = await _db.TenantMembers
            .Include(m => m.Tenant)
            .Where(m => m.UserId == user.Id)
            .ToListAsync(ct);

        foreach (var m in memberships)
        {
            if (m.Role != TenantRole.Owner || m.Tenant is null) continue;

            var otherOwnerCount = await _db.TenantMembers.CountAsync(
                t => t.TenantId == m.TenantId && t.UserId != user.Id && t.Role == TenantRole.Owner, ct);
            if (otherOwnerCount == 0 && m.Tenant.DeletedAt is null)
            {
                m.Tenant.DeletedAt = now;
            }
        }

        await _db.SaveChangesAsync(ct);
        await _signInManager.SignOutAsync();

        // Hard-delete scheduled for 30 days from now. Sweep not yet
        // implemented — will require a background job runner (Hangfire or
        // similar). Until then, rows remain soft-deleted.
        return Ok(new { message = "Account deleted. A hard-delete will run 30 days from now.", deletedAt = now });
    }

    private async Task<CurrentUserDto> BuildCurrentUserDto(ApplicationUser user)
    {
        _tenantContext.BypassFilter = true;
        var memberships = await _db.TenantMembers
            .Where(m => m.UserId == user.Id)
            .Include(m => m.Tenant)
            .Where(m => m.Tenant!.DeletedAt == null)
            .Select(m => new TenantMembershipDto(m.TenantId, m.Tenant!.Name, m.Tenant!.Slug, m.Role))
            .ToListAsync();

        int? currentTenantId = null;
        var claim = User.FindFirstValue(TenantContextMiddleware.TenantClaimType);
        if (int.TryParse(claim, out var tid)) currentTenantId = tid;

        // Enabled features + billing snapshot in one tenant load. Empty when no
        // tenant is selected.
        List<string> enabledFeatures = new();
        BillingSnapshotDto? billing = null;
        if (currentTenantId is int tenantId)
        {
            var tenant = await _db.Tenants
                .Include(t => t.Plan).ThenInclude(p => p!.Features)
                .FirstOrDefaultAsync(t => t.Id == tenantId);
            if (tenant?.Plan is not null)
            {
                enabledFeatures = tenant.Plan.Features
                    .Where(f => f.Enabled)
                    .Select(f => f.Feature.ToString())
                    .ToList();
                billing = new BillingSnapshotDto(
                    tenant.Plan.Name,
                    tenant.Plan.Slug,
                    tenant.SubscriptionStatus,
                    tenant.TrialEndsAt,
                    tenant.CurrentPeriodEnd);
            }
        }

        return new CurrentUserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            currentTenantId,
            memberships,
            user.IsSuperAdmin,
            enabledFeatures,
            billing);
    }

    private static IEnumerable<Claim> BuildClaims(ApplicationUser user, int? tenantId)
    {
        if (tenantId is int tid)
            yield return new Claim(TenantContextMiddleware.TenantClaimType, tid.ToString());
        if (user.IsSuperAdmin)
            yield return new Claim(SuperAdminPolicy.ClaimType, "true");
    }

    // ----- Email confirmation + password reset -----

    public record EmailRequest(string Email);
    public record ConfirmEmailRequest(string UserId, string Token);
    public record ResetPasswordRequest(string UserId, string Token, string NewPassword);

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] EmailRequest req)
    {
        // Always succeed to avoid leaking which emails are registered. If the
        // email actually matches an active account, we send a reset link.
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user is not null && user.DeletedAt is null)
        {
            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var url = BuildClientUrl("reset-password", user.Id, token);
                var tpl = EmailTemplates.PasswordReset(user.DisplayName, url);
                await _email.SendAsync(user.Email!, tpl.Subject, tpl.Html, tpl.Text);
            }
            catch (Exception ex) { _logger.LogError(ex, "Password reset email send failed for {Email}", req.Email); /* don't reveal to caller */ }
        }
        return Ok(new { message = "If that email is on file, a reset link has been sent." });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var user = await _userManager.FindByIdAsync(req.UserId);
        if (user is null) return BadRequest(new { error = "Invalid reset link." });

        var decodedToken = DecodeToken(req.Token);
        var result = await _userManager.ResetPasswordAsync(user, decodedToken, req.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return Ok(new { message = "Password updated. You can sign in now." });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest req)
    {
        var user = await _userManager.FindByIdAsync(req.UserId);
        if (user is null) return BadRequest(new { error = "Invalid confirmation link." });

        var decodedToken = DecodeToken(req.Token);
        var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return Ok(new { message = "Email confirmed. You can sign in now." });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation([FromBody] EmailRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user is not null && user.DeletedAt is null && !await _userManager.IsEmailConfirmedAsync(user))
        {
            try { await SendConfirmationEmailAsync(user); }
            catch { /* swallow to avoid leaking state */ }
        }
        return Ok(new { message = "If that email is on file and unconfirmed, we've sent a new link." });
    }

    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        if (user.Email is null) return;
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var url = BuildClientUrl("confirm-email", user.Id, token);
        var tpl = EmailTemplates.ConfirmEmail(user.DisplayName, url);
        await _email.SendAsync(user.Email, tpl.Subject, tpl.Html, tpl.Text);
    }

    // Identity tokens need to survive a URL round-trip without mangling.
    // Base64Url encode when putting into a query string, decode when reading.
    private string BuildClientUrl(string path, string userId, string token)
    {
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var origin = $"{Request.Scheme}://{Request.Host}";
        var userIdEnc = Uri.EscapeDataString(userId);
        return $"{origin}/{path}?userId={userIdEnc}&token={encodedToken}";
    }

    private static string DecodeToken(string encodedToken)
        => Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));

    private static string Slugify(string name)
    {
        var s = new string(name.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray());
        while (s.Contains("--")) s = s.Replace("--", "-");
        return s.Trim('-');
    }
}
