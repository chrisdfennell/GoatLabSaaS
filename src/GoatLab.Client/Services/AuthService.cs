using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GoatLab.Shared.DTOs;

namespace GoatLab.Client.Services;

/// <summary>
/// Client wrapper around /api/account endpoints. Uses cookie auth — the browser
/// attaches the auth cookie automatically on same-origin requests.
/// </summary>
public class AuthService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public AuthService(HttpClient http) => _http = http;

    public record RegisterResult(
        bool Ok,
        string? Error,
        CurrentUserDto? User,
        bool RequiresConfirmation,
        string? Email);

    public record LoginResult(
        bool Ok,
        string? Error,
        CurrentUserDto? User,
        bool EmailUnconfirmed,
        string? Email,
        bool RequiresTwoFactor = false,
        bool HasPasskeys = false);

    public async Task<CurrentUserDto?> GetCurrentUserAsync()
    {
        var response = await _http.GetAsync("api/account/me");
        if (response.StatusCode == HttpStatusCode.Unauthorized) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CurrentUserDto>();
    }

    public async Task<RegisterResult> RegisterAsync(RegisterRequest req)
    {
        var response = await _http.PostAsJsonAsync("api/account/register", req);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return new RegisterResult(false, body, null, false, null);

        // The server may return either the CurrentUserDto (auto-signed-in) or a
        // { requiresConfirmation, email, message } envelope.
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("requiresConfirmation", out var rc) && rc.GetBoolean())
        {
            var email = doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
            return new RegisterResult(true, null, null, true, email);
        }
        var user = JsonSerializer.Deserialize<CurrentUserDto>(body, JsonOpts);
        return new RegisterResult(true, null, user, false, user?.Email);
    }

    public async Task<LoginResult> LoginAsync(LoginRequest req)
    {
        var response = await _http.PostAsJsonAsync("api/account/login", req);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            // Server returns { error, emailUnconfirmed?, email? } on 401.
            try
            {
                using var doc = JsonDocument.Parse(body);
                var error = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : body;
                var unconfirmed = doc.RootElement.TryGetProperty("emailUnconfirmed", out var u) && u.GetBoolean();
                var email = doc.RootElement.TryGetProperty("email", out var em) ? em.GetString() : null;
                return new LoginResult(false, error, null, unconfirmed, email);
            }
            catch { return new LoginResult(false, body, null, false, null); }
        }
        // 200 + requiresTwoFactor means password was good but 2FA is needed.
        using var doc2 = JsonDocument.Parse(body);
        if (doc2.RootElement.TryGetProperty("requiresTwoFactor", out var rtf) && rtf.GetBoolean())
        {
            var hasPasskeys = doc2.RootElement.TryGetProperty("passkeys", out var pk)
                              && pk.ValueKind == JsonValueKind.Array && pk.GetArrayLength() > 0;
            return new LoginResult(true, null, null, false, null,
                RequiresTwoFactor: true, HasPasskeys: hasPasskeys);
        }
        var user = JsonSerializer.Deserialize<CurrentUserDto>(body, JsonOpts);
        return new LoginResult(true, null, user, false, user?.Email);
    }

    public async Task<CurrentUserDto?> SelectTenantAsync(int tenantId)
    {
        var response = await _http.PostAsJsonAsync("api/account/select-tenant", new SelectTenantRequest(tenantId));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CurrentUserDto>();
    }

    public async Task LogoutAsync()
    {
        await _http.PostAsync("api/account/logout", null);
    }

    public async Task<(bool ok, string? error)> ForgotPasswordAsync(string email)
    {
        var resp = await _http.PostAsJsonAsync("api/account/forgot-password", new { Email = email });
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync());
    }

    public async Task<(bool ok, string? error)> ResetPasswordAsync(string userId, string token, string newPassword)
    {
        var resp = await _http.PostAsJsonAsync("api/account/reset-password",
            new { UserId = userId, Token = token, NewPassword = newPassword });
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync());
    }

    public async Task<(bool ok, string? error)> ConfirmEmailAsync(string userId, string token)
    {
        var resp = await _http.PostAsJsonAsync("api/account/confirm-email",
            new { UserId = userId, Token = token });
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync());
    }

    public async Task<(bool ok, string? error)> ResendConfirmationAsync(string email)
    {
        var resp = await _http.PostAsJsonAsync("api/account/resend-confirmation", new { Email = email });
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync());
    }
}
