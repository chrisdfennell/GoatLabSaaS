using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace GoatLab.Client.Services;

/// <summary>
/// Wraps the goatlabRecaptcha JS interop. Lazy-loads the reCAPTCHA script the
/// first time a token is requested, so anonymous landing pages don't pay the
/// network cost unless the visitor actually submits something.
///
/// Returns null when reCAPTCHA isn't configured (empty SiteKey from the
/// server). Callers should still send the request — server-side verification
/// is also a no-op when the secret key isn't set, so dev/test stays usable.
/// </summary>
public class RecaptchaService
{
    private readonly IJSRuntime _js;
    private readonly HttpClient _http;
    private string? _siteKey;
    private bool _loadAttempted;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public RecaptchaService(IJSRuntime js, HttpClient http)
    {
        _js = js;
        _http = http;
    }

    public async Task<string?> ExecuteAsync(string action)
    {
        await EnsureLoadedAsync();
        if (string.IsNullOrEmpty(_siteKey)) return null;
        try
        {
            return await _js.InvokeAsync<string?>("goatlabRecaptcha.execute", action);
        }
        catch
        {
            return null;
        }
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loadAttempted) return;
        await _loadLock.WaitAsync();
        try
        {
            if (_loadAttempted) return;
            _loadAttempted = true;

            try
            {
                var resp = await _http.GetFromJsonAsync<RecaptchaKeyResponse>("api/config/recaptcha-key");
                _siteKey = resp?.SiteKey;
            }
            catch
            {
                _siteKey = null;
            }

            if (!string.IsNullOrEmpty(_siteKey))
            {
                try { await _js.InvokeVoidAsync("goatlabRecaptcha.load", _siteKey); }
                catch { /* JS load errors are non-fatal — token will be null */ }
            }
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private record RecaptchaKeyResponse(string SiteKey);
}
