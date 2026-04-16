using System.Net.Http.Json;
using System.Text.Json;

namespace GoatLab.Client.Services;

public class TwoFactorService
{
    private readonly HttpClient _http;
    public TwoFactorService(HttpClient http) => _http = http;

    public record Status(bool TotpEnabled, int RecoveryCodesRemaining, List<Passkey> Passkeys);
    public record Passkey(int Id, string Name, DateTime CreatedAt, DateTime? LastUsedAt);
    public record TotpSetup(string Secret, string AuthenticatorUri);
    public record EnableResult(bool Enabled, List<string> RecoveryCodes);

    public async Task<Status?> GetStatusAsync()
        => await _http.GetFromJsonAsync<Status>("api/account/two-factor/status");

    public async Task<TotpSetup?> SetupTotpAsync()
    {
        var res = await _http.PostAsync("api/account/two-factor/totp/setup", null);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<TotpSetup>();
    }

    public async Task<(bool ok, string? error, EnableResult? result)> EnableTotpAsync(string code)
    {
        var res = await _http.PostAsJsonAsync("api/account/two-factor/totp/enable", new { Code = code });
        if (!res.IsSuccessStatusCode) return (false, await res.Content.ReadAsStringAsync(), null);
        return (true, null, await res.Content.ReadFromJsonAsync<EnableResult>());
    }

    public async Task<(bool ok, string? error)> DisableTotpAsync(string code)
    {
        var res = await _http.PostAsJsonAsync("api/account/two-factor/totp/disable", new { Code = code });
        if (res.IsSuccessStatusCode) return (true, null);
        return (false, await res.Content.ReadAsStringAsync());
    }

    public async Task<List<string>> RegenerateRecoveryCodesAsync()
    {
        var res = await _http.PostAsync("api/account/two-factor/recovery-codes/regenerate", null);
        if (!res.IsSuccessStatusCode) return new();
        return await res.Content.ReadFromJsonAsync<List<string>>() ?? new();
    }

    // Passkey registration: start → JS interop → complete
    public async Task<string?> PasskeyRegisterStartAsync(string name)
    {
        var res = await _http.PostAsJsonAsync("api/account/two-factor/passkey/register-start", new { Name = name });
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadAsStringAsync(); // raw JSON for JS interop
    }

    public async Task<(bool ok, string? error)> PasskeyRegisterCompleteAsync(string attestationJson)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(new { response = JsonSerializer.Deserialize<JsonElement>(attestationJson) }),
            System.Text.Encoding.UTF8, "application/json");
        var res = await _http.PostAsync("api/account/two-factor/passkey/register-complete", content);
        if (res.IsSuccessStatusCode) return (true, null);
        return (false, await res.Content.ReadAsStringAsync());
    }

    public async Task<bool> DeletePasskeyAsync(int id)
    {
        var res = await _http.DeleteAsync($"api/account/two-factor/passkeys/{id}");
        return res.IsSuccessStatusCode;
    }

    // Login 2FA steps
    public async Task<(bool ok, string? error, string? userJson)> VerifyTotpLoginAsync(string code, bool rememberMe, bool rememberMachine)
    {
        var res = await _http.PostAsJsonAsync("api/account/login/verify-totp",
            new { Code = code, RememberMe = rememberMe, RememberMachine = rememberMachine });
        var body = await res.Content.ReadAsStringAsync();
        return (res.IsSuccessStatusCode, res.IsSuccessStatusCode ? null : body, res.IsSuccessStatusCode ? body : null);
    }

    public async Task<string?> PasskeyLoginStartAsync()
    {
        var res = await _http.PostAsync("api/account/login/passkey-start", null);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadAsStringAsync();
    }

    public async Task<(bool ok, string? error, string? userJson)> PasskeyLoginCompleteAsync(string assertionJson, bool rememberMe)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(new { response = JsonSerializer.Deserialize<JsonElement>(assertionJson), rememberMe }),
            System.Text.Encoding.UTF8, "application/json");
        var res = await _http.PostAsync("api/account/login/passkey-complete", content);
        var body = await res.Content.ReadAsStringAsync();
        return (res.IsSuccessStatusCode, res.IsSuccessStatusCode ? null : body, res.IsSuccessStatusCode ? body : null);
    }
}
