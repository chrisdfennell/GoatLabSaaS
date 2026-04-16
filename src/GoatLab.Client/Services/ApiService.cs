using System.Net;
using System.Net.Http.Json;
using GoatLab.Client.Components.Shared;
using Microsoft.JSInterop;
using MudBlazor;

namespace GoatLab.Client.Services;

/// <summary>
/// Generic HTTP helper wrapping the typed HttpClient.
///
/// Writes (POST/PUT/DELETE/PATCH) are offline-aware: when the browser is offline or
/// the network throws, we enqueue the op in IndexedDB and raise <see cref="OfflineQueuedException"/>.
/// Callers can catch that to show a "Queued for sync" toast instead of an error.
///
/// HTTP 402 (feature gated by the tenant's plan) is surfaced as a toast and
/// callers receive <c>default</c> — no exception — so feature-gated pages render
/// empty instead of showing the Blazor "unhandled error" banner.
/// </summary>
public class ApiService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly ISnackbar _snackbar;
    private readonly IDialogService _dialogs;
    // Prevent two upgrade dialogs piling on top of each other when a page fires
    // multiple requests in parallel (e.g. tabs, prefetch, OnInitialized).
    private bool _upgradeDialogOpen;

    public ApiService(HttpClient http, IJSRuntime js, ISnackbar snackbar, IDialogService dialogs)
    {
        _http = http;
        _js = js;
        _snackbar = snackbar;
        _dialogs = dialogs;
    }

    public async Task<T?> GetAsync<T>(string url)
    {
        var resp = await _http.GetAsync(url);
        if (resp.StatusCode == HttpStatusCode.PaymentRequired)
        {
            await ShowUpgradeToastAsync(resp);
            return default;
        }
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>();
    }

    public async Task<T?> PostAsync<T>(string url, T data)
    {
        HttpRequestException? networkErr = null;
        try
        {
            var resp = await _http.PostAsJsonAsync(url, data);
            if (await HandleUpgradeAsync(resp)) return default;
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
            if (await HandleUpgradeAsync(resp)) return;
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
            if (await HandleUpgradeAsync(resp)) return;
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
            if (await HandleUpgradeAsync(resp)) return;
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
            if (await HandleUpgradeAsync(resp)) return;
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
            if (await HandleUpgradeAsync(resp)) return default;
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
        if (await HandleUpgradeAsync(resp)) return default;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>();
    }

    public async Task<T?> PostFormAsync<T>(string url, MultipartFormDataContent form)
    {
        var resp = await _http.PostAsync(url, form);
        if (await HandleUpgradeAsync(resp)) return default;
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

    /// <summary>Returns true if the response was a 402 and has been toasted.</summary>
    private async Task<bool> HandleUpgradeAsync(HttpResponseMessage resp)
    {
        if (resp.StatusCode != HttpStatusCode.PaymentRequired) return false;
        await ShowUpgradeToastAsync(resp);
        return true;
    }

    private async Task ShowUpgradeToastAsync(HttpResponseMessage resp)
    {
        // Two 402s racing to open two dialogs would stack badly. First one wins;
        // subsequent ones fall through to a quiet snackbar.
        UpgradePayload? payload = null;
        try { payload = await resp.Content.ReadFromJsonAsync<UpgradePayload>(); }
        catch { /* non-JSON body — fall back to generic dialog */ }

        if (_upgradeDialogOpen)
        {
            _snackbar.Add(payload?.Error ?? "Upgrade your plan to access this.", Severity.Warning);
            return;
        }

        _upgradeDialogOpen = true;
        try
        {
            var parameters = new DialogParameters
            {
                ["Feature"] = payload?.Feature,
                ["Limit"] = payload?.Limit,
            };
            var dialog = await _dialogs.ShowAsync<UpgradeDialog>("Upgrade required", parameters,
                new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true });
            await dialog.Result;
        }
        finally { _upgradeDialogOpen = false; }
    }

    private record UpgradePayload(string? Error, string? Feature, string? Limit, bool UpgradeRequired);
}
