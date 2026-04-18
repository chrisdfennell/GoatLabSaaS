using System.Security.Claims;
using System.Text.Encodings.Web;
using GoatLab.Server.Data;
using GoatLab.Server.Services.ApiKeys;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoatLab.Server.Services.Auth;

// Custom authentication scheme for programmatic API access. Clients send
//   Authorization: Bearer gl_<secret>
// The handler hashes the plaintext, looks up a non-revoked ApiKey, and emits
// a principal carrying the tenant_id claim so TenantContextMiddleware does
// the right thing downstream. The cookie scheme still handles browser
// sessions; controllers that must stay cookie-only declare
// [Authorize(AuthenticationSchemes = IdentityConstants.ApplicationScheme)].
public class ApiKeyAuthOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
}

public class ApiKeyAuthHandler : AuthenticationHandler<ApiKeyAuthOptions>
{
    private readonly GoatLabDbContext _db;

    public ApiKeyAuthHandler(
        IOptionsMonitor<ApiKeyAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        GoatLabDbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            return AuthenticateResult.NoResult();

        var value = authHeader.ToString();
        const string bearer = "Bearer ";
        if (!value.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var plaintext = value[bearer.Length..].Trim();
        if (!plaintext.StartsWith(ApiKeyGenerator.Prefix, StringComparison.Ordinal))
            return AuthenticateResult.NoResult();

        var hash = ApiKeyGenerator.HashPlaintext(plaintext);
        var now = DateTime.UtcNow;

        // Tenant filter is keyed off the request's tenant context, which isn't
        // set yet (that's what we're about to do). Bypass to find the key.
        var apiKey = await _db.ApiKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.KeyHash == hash
                                      && k.RevokedAt == null
                                      && (k.ExpiresAt == null || k.ExpiresAt > now));

        if (apiKey is null) return AuthenticateResult.Fail("Invalid API key.");

        // Fire-and-forget timestamp update. Losing the write on process exit
        // is fine — LastUsedAt is best-effort telemetry.
        _ = Task.Run(async () =>
        {
            try
            {
                await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE ApiKeys SET LastUsedAt = {0} WHERE Id = {1}", now, apiKey.Id);
            }
            catch { /* swallow — transient DB blips shouldn't fail the request */ }
        });

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, $"apikey:{apiKey.Id}"),
            new Claim(ClaimTypes.Name, $"ApiKey:{apiKey.Name}"),
            new Claim("tenant_id", apiKey.TenantId.ToString()),
            new Claim("auth_method", "api_key"),
        };
        var identity = new ClaimsIdentity(claims, ApiKeyAuthOptions.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthOptions.SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
