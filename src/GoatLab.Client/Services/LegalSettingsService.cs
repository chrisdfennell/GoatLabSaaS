using System.Net.Http.Json;
using GoatLab.Shared.DTOs;

namespace GoatLab.Client.Services;

// Fetches /api/legal/settings once per page-load and substitutes lawyer-filled
// values into the Terms/Privacy templates. When a field is null/blank the page
// keeps the original "[BRACKETED PLACEHOLDER]" text so unset fields are obvious.
public class LegalSettingsService
{
    private readonly HttpClient _http;
    private LegalSettingsDto? _cached;
    private Task<LegalSettingsDto?>? _inFlight;

    public LegalSettingsService(HttpClient http) => _http = http;

    public Task<LegalSettingsDto?> GetAsync()
    {
        if (_cached is not null) return Task.FromResult<LegalSettingsDto?>(_cached);
        return _inFlight ??= LoadAsync();
    }

    private async Task<LegalSettingsDto?> LoadAsync()
    {
        try
        {
            _cached = await _http.GetFromJsonAsync<LegalSettingsDto>("api/legal/settings");
        }
        catch
        {
            // Network blip or server down: render with all placeholders. The
            // warning banner stays up — that is the safe default.
            _cached = new LegalSettingsDto(null, null, null, null, null, null, null, null, null, false);
        }
        return _cached;
    }

    // Helpers used by the Razor pages. Returns the value if set, else the
    // bracketed placeholder so unfilled fields remain obvious to a reader.
    public static string Or(string? value, string placeholder)
        => string.IsNullOrWhiteSpace(value) ? $"[{placeholder}]" : value;
}
