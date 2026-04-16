using System.Text;
using System.Text.Encodings.Web;
using Fido2NetLib;
using Fido2NetLib.Objects;
using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GoatLab.Server.Controllers;

// TOTP (authenticator app) + WebAuthn passkey management. Users can enable
// either, both, or neither — both are optional second factors that trigger at
// login after password check passes. Recovery codes are generated whenever
// TOTP is enabled and regenerated on demand.
[ApiController]
[Route("api/account/two-factor")]
public class TwoFactorController : ControllerBase
{
    private const int RecoveryCodeCount = 10;
    private static readonly TimeSpan ChallengeTtl = TimeSpan.FromMinutes(5);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly GoatLabDbContext _db;
    private readonly IFido2 _fido2;
    private readonly IMemoryCache _cache;
    private readonly UrlEncoder _url;

    public TwoFactorController(
        UserManager<ApplicationUser> userManager,
        GoatLabDbContext db,
        IFido2 fido2,
        IMemoryCache cache,
        UrlEncoder url)
    {
        _userManager = userManager;
        _db = db;
        _fido2 = fido2;
        _cache = cache;
        _url = url;
    }

    // ----- Shared -----

    public record StatusDto(bool TotpEnabled, int RecoveryCodesRemaining, List<PasskeyDto> Passkeys);

    public record PasskeyDto(int Id, string Name, DateTime CreatedAt, DateTime? LastUsedAt);

    [HttpGet("status")]
    public async Task<ActionResult<StatusDto>> GetStatus()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var codes = await _userManager.CountRecoveryCodesAsync(user);
        var passkeys = await _db.UserCredentials
            .Where(c => c.UserId == user.Id)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new PasskeyDto(c.Id, c.Name, c.CreatedAt, c.LastUsedAt))
            .ToListAsync();

        return new StatusDto(user.TwoFactorEnabled, codes, passkeys);
    }

    // ----- TOTP -----

    public record TotpSetupDto(string Secret, string AuthenticatorUri);
    public record TotpCodeRequest(string Code);
    public record EnableTotpResult(bool Enabled, List<string> RecoveryCodes);

    [HttpPost("totp/setup")]
    public async Task<ActionResult<TotpSetupDto>> TotpSetup()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var uri = BuildAuthenticatorUri(user.Email ?? user.UserName ?? "user", key!);
        return new TotpSetupDto(FormatKey(key!), uri);
    }

    [HttpPost("totp/enable")]
    public async Task<ActionResult<EnableTotpResult>> TotpEnable([FromBody] TotpCodeRequest req)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var code = (req.Code ?? "").Replace(" ", "").Replace("-", "");
        var ok = await _userManager.VerifyTwoFactorTokenAsync(user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider, code);
        if (!ok) return BadRequest(new { error = "Invalid code. Try again with a fresh 6-digit code." });

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        return new EnableTotpResult(true, recoveryCodes?.ToList() ?? new());
    }

    [HttpPost("totp/disable")]
    public async Task<IActionResult> TotpDisable([FromBody] TotpCodeRequest req)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        if (!user.TwoFactorEnabled) return Ok(new { message = "Already disabled." });

        var code = (req.Code ?? "").Replace(" ", "").Replace("-", "");
        var ok = await _userManager.VerifyTwoFactorTokenAsync(user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider, code);
        if (!ok) return BadRequest(new { error = "Invalid code — can't disable without current verification." });

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);
        return Ok(new { disabled = true });
    }

    [HttpPost("recovery-codes/regenerate")]
    public async Task<ActionResult<List<string>>> RegenerateRecoveryCodes()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        if (!user.TwoFactorEnabled) return BadRequest(new { error = "Enable 2FA first." });

        var codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        return Ok(codes?.ToList() ?? new List<string>());
    }

    // otpauth://totp/GoatLab:chris@x.com?secret=ABCDE&issuer=GoatLab
    private string BuildAuthenticatorUri(string email, string key)
        => $"otpauth://totp/{_url.Encode("GoatLab")}:{_url.Encode(email)}?secret={key}&issuer={_url.Encode("GoatLab")}&digits=6";

    private static string FormatKey(string key)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < key.Length; i += 4)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(key.AsSpan(i, Math.Min(4, key.Length - i)));
        }
        return sb.ToString().ToLowerInvariant();
    }

    // ----- Passkeys (WebAuthn registration) -----

    public record RegisterStartRequest(string Name);

    [HttpPost("passkey/register-start")]
    public async Task<ActionResult<CredentialCreateOptions>> PasskeyRegisterStart([FromBody] RegisterStartRequest req)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var existing = await _db.UserCredentials
            .Where(c => c.UserId == user.Id)
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToListAsync();

        var fidoUser = new Fido2User
        {
            Id = Encoding.UTF8.GetBytes(user.Id),
            Name = user.Email ?? user.UserName ?? "user",
            DisplayName = user.DisplayName,
        };

        var options = _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = fidoUser,
            ExcludeCredentials = existing,
            AuthenticatorSelection = AuthenticatorSelection.Default,
            AttestationPreference = AttestationConveyancePreference.None,
        });

        _cache.Set(RegistrationKey(user.Id), (options.ToJson(), req.Name ?? "Passkey"), ChallengeTtl);
        return options;
    }

    public record RegisterCompleteRequest(AuthenticatorAttestationRawResponse Response);

    [HttpPost("passkey/register-complete")]
    public async Task<ActionResult<PasskeyDto>> PasskeyRegisterComplete([FromBody] RegisterCompleteRequest req)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        if (!_cache.TryGetValue<(string Json, string Name)>(RegistrationKey(user.Id), out var stash))
            return BadRequest(new { error = "Registration challenge expired. Start again." });

        var options = CredentialCreateOptions.FromJson(stash.Json);

        var registered = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = req.Response,
            OriginalOptions = options,
            IsCredentialIdUniqueToUserCallback = async (args, ct) =>
                !await _db.UserCredentials.AnyAsync(c => c.CredentialId == args.CredentialId, ct),
        });

        var cred = new UserCredential
        {
            UserId = user.Id,
            CredentialId = registered.Id,
            PublicKey = registered.PublicKey,
            SignCount = registered.SignCount,
            Name = string.IsNullOrWhiteSpace(stash.Name) ? "Passkey" : stash.Name.Trim(),
            AaGuid = registered.AaGuid,
            Transports = registered.Transports is { Length: > 0 } t
                ? string.Join(',', t.Select(x => x.ToString()))
                : null,
        };
        _db.UserCredentials.Add(cred);
        await _db.SaveChangesAsync();
        _cache.Remove(RegistrationKey(user.Id));

        return new PasskeyDto(cred.Id, cred.Name, cred.CreatedAt, cred.LastUsedAt);
    }

    [HttpDelete("passkeys/{id:int}")]
    public async Task<IActionResult> DeletePasskey(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var cred = await _db.UserCredentials.FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);
        if (cred is null) return NotFound();

        _db.UserCredentials.Remove(cred);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    internal static string RegistrationKey(string userId) => $"webauthn:reg:{userId}";
}
