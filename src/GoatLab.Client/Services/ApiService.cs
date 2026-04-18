using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GoatLab.Client.Components.Shared;
using Microsoft.JSInterop;
using MudBlazor;

namespace GoatLab.Client.Services;

/// <summary>
/// Generic HTTP helper wrapping the typed HttpClient.
///
/// Writes (POST/PUT/DELETE/PATCH) are offline-aware: when the browser is offline
/// we enqueue the op in IndexedDB and raise <see cref="OfflineQueuedException"/>
/// so callers can show a "Queued for sync" toast instead of an error.
///
/// Non-success HTTP responses on writes (400/403/404/409/422/500…) are surfaced
/// as a snackbar toast and the caller receives <c>default</c> — no exception —
/// so a single bad request doesn't crash the Blazor circuit. HTTP 402 keeps its
/// dedicated upgrade-dialog path.
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
        HttpResponseMessage resp;
        try { resp = await _http.PostAsJsonAsync(url, data); }
        catch (HttpRequestException ex)
        {
            await HandleTransportErrorAsync("POST", url, data, ex);
            return default;
        }

        if (await HandleUpgradeAsync(resp)) return default;
        if (await HandleClientErrorAsync(resp)) return default;
        return await resp.Content.ReadFromJsonAsync<T>();
    }

    public async Task PostAsync<T>(string url, T data, bool noReturn = true)
    {
        HttpResponseMessage resp;
        try { resp = await _http.PostAsJsonAsync(url, data); }
        catch (HttpRequestException ex)
        {
            await HandleTransportErrorAsync("POST", url, data, ex);
            return;
        }

        if (await HandleUpgradeAsync(resp)) return;
        if (await HandleClientErrorAsync(resp)) return;
    }

    public async Task PutAsync<T>(string url, T data)
    {
        HttpResponseMessage resp;
        try { resp = await _http.PutAsJsonAsync(url, data); }
        catch (HttpRequestException ex)
        {
            await HandleTransportErrorAsync("PUT", url, data, ex);
            return;
        }

        if (await HandleUpgradeAsync(resp)) return;
        if (await HandleClientErrorAsync(resp)) return;
    }

    public async Task DeleteAsync(string url)
    {
        HttpResponseMessage resp;
        try { resp = await _http.DeleteAsync(url); }
        catch (HttpRequestException ex)
        {
            await HandleTransportErrorAsync("DELETE", url, null, ex);
            return;
        }

        if (await HandleUpgradeAsync(resp)) return;
        if (await HandleClientErrorAsync(resp)) return;
    }

    public async Task PatchJsonAsync<T>(string url, T data)
    {
        var req = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = JsonContent.Create(data)
        };
        HttpResponseMessage resp;
        try { resp = await _http.SendAsync(req); }
        catch (HttpRequestException ex)
        {
            await HandleTransportErrorAsync("PATCH", url, data, ex);
            return;
        }

        if (await HandleUpgradeAsync(resp)) return;
        if (await HandleClientErrorAsync(resp)) return;
    }

    // Transport-level failure (DNS, connection refused, abort). If the PWA
    // reports offline we enqueue for sync and raise OfflineQueuedException so
    // callers that catch it can show a "queued" toast instead of an error.
    // If we're nominally online, surface a single error toast rather than
    // rethrow — a rethrown HttpRequestException here would crash the Blazor
    // circuit with the generic unhandled-error overlay.
    private async Task HandleTransportErrorAsync(string method, string url, object? body, HttpRequestException _)
    {
        if (await IsOfflineAsync())
        {
            await EnqueueAsync(method, url, body);
            throw new OfflineQueuedException("Saved offline — will sync when you're back online.");
        }
        _snackbar.Add("Network error. Please try again.", Severity.Error);
    }

    // Non-success HTTP status (not 402, which has its own dialog path). Parses
    // the error message from the response body (ProblemDetails / { error }
    // shapes), shows a toast, and returns true so the caller short-circuits
    // with default/skip.
    private async Task<bool> HandleClientErrorAsync(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return false;
        if (resp.StatusCode == HttpStatusCode.PaymentRequired) return false;

        var message = await ExtractErrorMessageAsync(resp);
        _snackbar.Add(message, Severity.Error);
        return true;
    }

    private static async Task<string> ExtractErrorMessageAsync(HttpResponseMessage resp)
    {
        // Try common JSON shapes from ASP.NET Core + our own controllers, fall
        // back to a generic status line when the body isn't JSON.
        try
        {
            using var stream = await resp.Content.ReadAsStreamAsync();
            if (stream.Length == 0) return DefaultMessage(resp);
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            // Our custom { error = "..." } shape (e.g. WaitlistController, AdminController).
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("error", out var errProp)
                && errProp.ValueKind == JsonValueKind.String)
            {
                var msg = errProp.GetString();
                if (!string.IsNullOrWhiteSpace(msg)) return msg!;
            }

            // ASP.NET Core ValidationProblemDetails { errors: { Field: ["msg"] } }.
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("errors", out var errsProp)
                && errsProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in errsProp.EnumerateObject())
                {
                    if (field.Value.ValueKind == JsonValueKind.Array && field.Value.GetArrayLength() > 0)
                    {
                        var msg = field.Value[0].GetString();
                        if (!string.IsNullOrWhiteSpace(msg)) return msg!;
                    }
                }
            }

            // ProblemDetails { title: "..." } fallback.
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("title", out var titleProp)
                && titleProp.ValueKind == JsonValueKind.String)
            {
                var msg = titleProp.GetString();
                if (!string.IsNullOrWhiteSpace(msg)) return msg!;
            }
        }
        catch { /* body wasn't JSON or was malformed — fall through */ }

        return DefaultMessage(resp);
    }

    private static string DefaultMessage(HttpResponseMessage resp) => resp.StatusCode switch
    {
        HttpStatusCode.NotFound => "Not found.",
        HttpStatusCode.Unauthorized => "You're not signed in.",
        HttpStatusCode.Forbidden => "You don't have permission to do that.",
        HttpStatusCode.Conflict => "That conflicts with an existing record.",
        HttpStatusCode.TooManyRequests => "Too many requests — slow down and try again.",
        >= HttpStatusCode.InternalServerError => "The server hit an error. Please try again.",
        _ => $"Request failed ({(int)resp.StatusCode} {resp.ReasonPhrase}).",
    };

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
        HttpResponseMessage resp;
        try { resp = await _http.PostAsJsonAsync(url, body); }
        catch (HttpRequestException ex)
        {
            await HandleTransportErrorAsync("POST", url, body, ex);
            return default;
        }

        if (await HandleUpgradeAsync(resp)) return default;
        if (await HandleClientErrorAsync(resp)) return default;
        return await resp.Content.ReadFromJsonAsync<TResp>();
    }

    public async Task<T?> PostEmptyAsync<T>(string url)
    {
        HttpResponseMessage resp;
        try { resp = await _http.PostAsync(url, null); }
        catch (HttpRequestException ex)
        {
            await HandleTransportErrorAsync("POST", url, null, ex);
            return default;
        }

        if (await HandleUpgradeAsync(resp)) return default;
        if (await HandleClientErrorAsync(resp)) return default;
        return await resp.Content.ReadFromJsonAsync<T>();
    }

    public async Task<T?> PostFormAsync<T>(string url, MultipartFormDataContent form)
    {
        HttpResponseMessage resp;
        try { resp = await _http.PostAsync(url, form); }
        catch (HttpRequestException ex)
        {
            // File uploads aren't offline-queueable (body is a stream), so just
            // surface a toast and bail.
            await HandleTransportErrorAsync("POST", url, null, ex);
            return default;
        }

        if (await HandleUpgradeAsync(resp)) return default;
        if (await HandleClientErrorAsync(resp)) return default;
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
