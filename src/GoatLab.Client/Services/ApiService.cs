using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace GoatLab.Client.Services;

/// <summary>
/// Generic HTTP helper wrapping the typed HttpClient.
/// Writes (POST/PUT/DELETE/PATCH) are offline-aware: when the browser is offline or
/// the network throws, we enqueue the op in IndexedDB and raise <see cref="OfflineQueuedException"/>.
/// Callers can catch that to show a "Queued for sync" toast instead of an error.
/// </summary>
public class ApiService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    public ApiService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public Task<T?> GetAsync<T>(string url) => _http.GetFromJsonAsync<T>(url);

    public async Task<T?> PostAsync<T>(string url, T data)
    {
        HttpRequestException? networkErr = null;
        try
        {
            var resp = await _http.PostAsJsonAsync(url, data);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<T>();
        }
        catch (HttpRequestException ex) { networkErr = ex; }
        await HandleOfflineAsync("POST", url, data, networkErr);
        return default;
    }

    public async Task PostAsync<T>(string url, T data, bool noReturn = true)
    {
        HttpRequestException? networkErr = null;
        try
        {
            var resp = await _http.PostAsJsonAsync(url, data);
            resp.EnsureSuccessStatusCode();
            return;
        }
        catch (HttpRequestException ex) { networkErr = ex; }
        await HandleOfflineAsync("POST", url, data, networkErr);
    }

    public async Task PutAsync<T>(string url, T data)
    {
        HttpRequestException? networkErr = null;
        try
        {
            var resp = await _http.PutAsJsonAsync(url, data);
            resp.EnsureSuccessStatusCode();
            return;
        }
        catch (HttpRequestException ex) { networkErr = ex; }
        await HandleOfflineAsync("PUT", url, data, networkErr);
    }

    public async Task DeleteAsync(string url)
    {
        HttpRequestException? networkErr = null;
        try
        {
            var resp = await _http.DeleteAsync(url);
            resp.EnsureSuccessStatusCode();
            return;
        }
        catch (HttpRequestException ex) { networkErr = ex; }
        await HandleOfflineAsync("DELETE", url, null, networkErr);
    }

    public async Task PatchJsonAsync<T>(string url, T data)
    {
        HttpRequestException? networkErr = null;
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = JsonContent.Create(data)
            };
            var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            return;
        }
        catch (HttpRequestException ex) { networkErr = ex; }
        await HandleOfflineAsync("PATCH", url, data, networkErr);
    }

    private async Task HandleOfflineAsync(string method, string url, object? body, HttpRequestException networkErr)
    {
        if (await IsOfflineAsync())
        {
            await EnqueueAsync(method, url, body);
            throw new OfflineQueuedException("Saved offline — will sync when you're back online.");
        }
        throw networkErr;
    }

    private async Task<bool> IsOfflineAsync()
    {
        try { return !(await _js.InvokeAsync<bool>("goatPwa.isOnline")); }
        catch { return false; }
    }

    private async Task EnqueueAsync(string method, string url, object? body)
    {
        var fullUrl = url.StartsWith("http") ? url : new Uri(_http.BaseAddress!, url).ToString();
        try { await _js.InvokeAsync<int>("goatOfflineQueue.enqueue", method, fullUrl, body); }
        catch { /* if the queue itself fails (e.g. SSR), swallow — the exception below conveys failure */ }
    }

    public async Task<TResp?> PostAsync<TBody, TResp>(string url, TBody body)
    {
        HttpRequestException? networkErr = null;
        try
        {
            var resp = await _http.PostAsJsonAsync(url, body);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<TResp>();
        }
        catch (HttpRequestException ex) { networkErr = ex; }
        await HandleOfflineAsync("POST", url, body, networkErr);
        return default;
    }

    public async Task<T?> PostEmptyAsync<T>(string url)
    {
        var resp = await _http.PostAsync(url, null);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>();
    }

    public async Task<T?> PostFormAsync<T>(string url, MultipartFormDataContent form)
    {
        var resp = await _http.PostAsync(url, form);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>();
    }

    public async Task<byte[]> GetBytesAsync(string url)
    {
        return await _http.GetByteArrayAsync(url);
    }

    public async Task<string> GetStringAsync(string url)
    {
        return await _http.GetStringAsync(url);
    }

    public HttpClient Http => _http;
}
