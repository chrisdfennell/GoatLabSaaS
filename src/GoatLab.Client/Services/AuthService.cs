using System.Net;
using System.Net.Http.Json;
using GoatLab.Shared.DTOs;

namespace GoatLab.Client.Services;

/// <summary>
/// Client wrapper around /api/account endpoints. Uses cookie auth — the browser
/// attaches the auth cookie automatically on same-origin requests.
/// </summary>
public class AuthService
{
    private readonly HttpClient _http;

    public AuthService(HttpClient http) => _http = http;

    public async Task<CurrentUserDto?> GetCurrentUserAsync()
    {
        var response = await _http.GetAsync("api/account/me");
        if (response.StatusCode == HttpStatusCode.Unauthorized) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CurrentUserDto>();
    }

    public async Task<(bool ok, string? error, CurrentUserDto? user)> RegisterAsync(RegisterRequest req)
    {
        var response = await _http.PostAsJsonAsync("api/account/register", req);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            return (false, body, null);
        }
        var user = await response.Content.ReadFromJsonAsync<CurrentUserDto>();
        return (true, null, user);
    }

    public async Task<(bool ok, string? error, CurrentUserDto? user)> LoginAsync(LoginRequest req)
    {
        var response = await _http.PostAsJsonAsync("api/account/login", req);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            return (false, body, null);
        }
        var user = await response.Content.ReadFromJsonAsync<CurrentUserDto>();
        return (true, null, user);
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
}
