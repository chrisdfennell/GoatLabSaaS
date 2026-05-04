using System.Security.Claims;
using System.Security.Cryptography;
using GoatLab.Server.Data;
using GoatLab.Server.Filters;
using GoatLab.Server.Services.Email;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

// Marketplace buyer auth — passwordless, email magic-link.
//
// Flow:
//   1. Buyer hits POST /api/buyer/auth/start with their email
//   2. We mint a one-time signin token (random 24 bytes, hex), store the
//      SHA-256 hash, and email the plaintext as a /buyer/auth/verify link
//   3. Buyer clicks the link → GET /buyer/auth/verify?token=X validates
//      the hash, finds-or-creates a BuyerAccount, signs them into the
//      "Buyer" cookie scheme, redirects to /buyer/dashboard
//   4. Cookie auth keeps them signed in for 60 days (sliding)
//
// reCAPTCHA on /start so the magic-link mailer isn't an open relay for
// abuse; verify endpoint relies on token entropy + 30-min expiry.
[AllowAnonymous]
public class BuyerAuthController : Controller
{
    private readonly GoatLabDbContext _db;
    private readonly IAppEmailSender _email;
    private readonly ILogger<BuyerAuthController> _log;

    public BuyerAuthController(GoatLabDbContext db, IAppEmailSender email, ILogger<BuyerAuthController> log)
    {
        _db = db;
        _email = email;
        _log = log;
    }

    public record StartRequest(string Email, string? ReturnUrl);
    public record StartResponse(bool Ok, string Message);

    private const int TokenLifetimeMinutes = 30;

    [HttpPost("/api/buyer/auth/start")]
    [RequireRecaptcha("buyer_signin")]
    public async Task<ActionResult<StartResponse>> Start(
        [FromBody] StartRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
            return BadRequest(new StartResponse(false, "Please enter a valid email address."));

        var emailNorm = req.Email.Trim().ToLowerInvariant();

        // Mint a token + store its hash. Plaintext only ever leaves in the
        // email body. Old unused tokens for the same email get invalidated
        // so a fresh "send me a link" supersedes any stale ones.
        await _db.BuyerSignInTokens
            .Where(t => t.Email == emailNorm && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.UsedAt, DateTime.UtcNow), ct);

        var plaintext = NewToken();
        var hash = HashToken(plaintext);

        _db.BuyerSignInTokens.Add(new BuyerSignInToken
        {
            TokenHash = hash,
            Email = emailNorm,
            ExpiresAt = DateTime.UtcNow.AddMinutes(TokenLifetimeMinutes),
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        try
        {
            var origin = $"{Request.Scheme}://{Request.Host}";
            var rt = string.IsNullOrEmpty(req.ReturnUrl) ? "/buyer/dashboard" : req.ReturnUrl;
            var verifyUrl = $"{origin}/buyer/auth/verify?token={plaintext}&returnUrl={Uri.EscapeDataString(rt)}";
            var (subject, html, text) = EmailTemplates.BuyerMagicLink(emailNorm, verifyUrl, TokenLifetimeMinutes);
            await _email.SendAsync(emailNorm, subject, html, text, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send buyer magic-link email to {Email}", emailNorm);
            // Don't reveal email-send failures to caller — same response
            // either way so we don't leak whether the address is deliverable.
        }

        return new StartResponse(true, "Check your email for the sign-in link. It expires in 30 minutes.");
    }

    [HttpGet("/buyer/auth/verify")]
    public async Task<IActionResult> Verify(string token, string? returnUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return View("VerifyFailed");

        var hash = HashToken(token);
        var row = await _db.BuyerSignInTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (row is null || row.UsedAt is not null || row.ExpiresAt < DateTime.UtcNow)
            return View("VerifyFailed");

        // Find-or-create the buyer account.
        var account = await _db.BuyerAccounts.FirstOrDefaultAsync(a => a.Email == row.Email, ct);
        if (account is null)
        {
            account = new BuyerAccount
            {
                Email = row.Email,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
            };
            _db.BuyerAccounts.Add(account);
        }
        else
        {
            account.LastLoginAt = DateTime.UtcNow;
        }

        row.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var claims = new List<Claim>
        {
            new("buyer_id", account.Id.ToString()),
            new(ClaimTypes.Email, account.Email),
        };
        var identity = new ClaimsIdentity(claims, "Buyer");
        await HttpContext.SignInAsync("Buyer", new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(60),
            });

        var safeReturn = !string.IsNullOrEmpty(returnUrl)
                         && returnUrl.StartsWith('/')
                         && !returnUrl.StartsWith("//")
            ? returnUrl
            : "/buyer/dashboard";
        return Redirect(safeReturn);
    }

    [HttpPost("/api/buyer/auth/logout")]
    [Microsoft.AspNetCore.Mvc.ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Buyer");
        return NoContent();
    }

    [HttpGet("/buyer/sign-in")]
    public IActionResult SignInPage(string? returnUrl)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    private static string NewToken()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string HashToken(string plaintext)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
