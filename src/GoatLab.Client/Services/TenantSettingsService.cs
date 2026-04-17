using System.Net.Http.Json;
using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class TenantSettingsService
{
    private readonly HttpClient _http;
    public TenantSettingsService(HttpClient http) => _http = http;

    public record Settings(int Id, string Name, string Slug, string? Location, TenantUnits Units, bool AlertEmailEnabled, bool PublicProfileEnabled, string? PublicContactEmail, DateTime CreatedAt);
    public record UpdateInput(string Name, string? Location, TenantUnits Units, bool AlertEmailEnabled, bool PublicProfileEnabled, string? PublicContactEmail);

    public async Task<Settings?> GetAsync()
        => await _http.GetFromJsonAsync<Settings>("api/tenant");

    public async Task<(bool ok, string? error, Settings? settings)> UpdateAsync(UpdateInput input)
    {
        var res = await _http.PutAsJsonAsync("api/tenant", input);
        if (!res.IsSuccessStatusCode)
        {
            return (false, await res.Content.ReadAsStringAsync(), null);
        }
        return (true, null, await res.Content.ReadFromJsonAsync<Settings>());
    }
}
