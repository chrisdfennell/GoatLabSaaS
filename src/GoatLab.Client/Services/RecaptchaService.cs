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
///
/// Retries the site-key fetch on transient failures: if the first
/// /api/config/recaptcha-key call fails (network blip on a slow mobile
/// network), we don't permanently give up — the next ExecuteAsync call
/// re-attempts. Once we successfully cache a non-empty key we stop retrying.
/// </summary>
public class RecaptchaService
{
    private readonly IJSRuntime _js;
    private readonly HttpClient _http;
    private string? _siteKey;
    private bool _siteKeyResolved; // true after we've confirmed key is empty (dev) or loaded
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
        if (_siteKeyResolved) return;
        await _loadLock.WaitAsync();
        try
        {
            if (_siteKeyResolved) return;

            try
            {
                var resp = await _http.GetFromJsonAsync<RecaptchaKeyResponse>("api/config/recaptcha-key");
                _siteKey = resp?.SiteKey;
                // Treat the call as resolved either way — if the server is
                // configured (returns a key), we won't retry. If it returns
                // empty (dev), we won't retry either; both states are stable.
                _siteKeyResolved = true;
            }
            catch
            {
                // Transient failure (DNS hiccup, brief CDN issue). Don't
                // mark resolved — the next ExecuteAsync will retry.
                _siteKey = null;
                return;
            }

            if (!string.IsNullOrEmpty(_siteKey))
            {
                try { await _js.InvokeVoidAsync("goatlabRecaptcha.load", _siteKey); }
                catch { /* JS load errors are non-fatal — execute() retries on its own promise */ }
            }
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private record RecaptchaKeyResponse(string SiteKey);
}
