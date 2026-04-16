using System.Net.Http.Json;

namespace GoatLab.Client.Services;

/// <summary>
/// Client wrapper for /api/config/* — server-provided configuration the client
/// needs at runtime. Today that's just the Google Maps JavaScript key; other
/// server-authoritative settings can pile on here later.
/// </summary>
public class ConfigService
{
    private readonly HttpClient _http;
    public ConfigService(HttpClient http) => _http = http;

    public async Task<string?> GetGoogleMapsKeyAsync()
    {
        try
        {
            var resp = await _http.GetFromJsonAsync<GoogleMapsKeyResponse>("api/config/google-maps-key");
            return resp?.ApiKey;
        }
        catch
        {
            return null;
        }
    }

    private record GoogleMapsKeyResponse(string ApiKey);
}
