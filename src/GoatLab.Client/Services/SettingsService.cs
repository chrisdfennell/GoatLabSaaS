using System.Net;
using System.Net.Http.Json;

namespace GoatLab.Client.Services;

public class SettingsService
{
    private readonly HttpClient _http;
    public SettingsService(HttpClient http) => _http = http;

    public async Task<string?> GetAsync(string key)
    {
        var resp = await _http.GetAsync($"api/settings/{key}");
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<string>();
    }

    public async Task SetAsync(string key, string? value)
    {
        var resp = await _http.PutAsJsonAsync($"api/settings/{key}", value);
        resp.EnsureSuccessStatusCode();
    }

    public const string GoogleMapsApiKey = "googleMapsApiKey";
}
