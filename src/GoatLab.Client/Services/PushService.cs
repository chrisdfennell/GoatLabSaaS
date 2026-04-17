using System.Text.Json;
using GoatLab.Shared.DTOs;
using Microsoft.JSInterop;

namespace GoatLab.Client.Services;

public class PushService
{
    private readonly ApiService _api;
    private readonly IJSRuntime _js;

    public PushService(ApiService api, IJSRuntime js)
    {
        _api = api;
        _js = js;
    }

    public Task<bool> IsSupportedAsync() => _js.InvokeAsync<bool>("goatPush.isSupported").AsTask();
    public Task<string> PermissionAsync() => _js.InvokeAsync<string>("goatPush.permission").AsTask();
    public Task<string?> CurrentEndpointAsync() => _js.InvokeAsync<string?>("goatPush.currentEndpoint").AsTask();

    /// <summary>Subscribe this browser/device. Returns true on success.</summary>
    public async Task<bool> SubscribeAsync()
    {
        var keyResp = await _api.GetAsync<VapidPublicKeyResponse>("api/push/vapid-public-key");
        if (keyResp is null || string.IsNullOrEmpty(keyResp.PublicKey)) return false;

        // The browser returns a JSON-stringified subscription envelope; we
        // parse it back so we can pull endpoint + keys into the typed DTO.
        var rawJson = await _js.InvokeAsync<string?>("goatPush.subscribe", keyResp.PublicKey);
        if (string.IsNullOrEmpty(rawJson)) return false;

        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;
        var endpoint = root.GetProperty("endpoint").GetString() ?? "";
        var keys = root.GetProperty("keys");
        var p256dh = keys.GetProperty("p256dh").GetString() ?? "";
        var auth = keys.GetProperty("auth").GetString() ?? "";
        string? userAgent = null;
        try { userAgent = await _js.InvokeAsync<string?>("eval", "navigator.userAgent"); } catch { }

        await _api.PostAsync("api/push/subscribe", new PushSubscribeRequest(endpoint, p256dh, auth, userAgent));
        return true;
    }

    public async Task UnsubscribeAsync()
    {
        var endpoint = await CurrentEndpointAsync();
        await _js.InvokeVoidAsync("goatPush.unsubscribe");
        if (!string.IsNullOrEmpty(endpoint))
            await _api.PostAsync<object>("api/push/unsubscribe", new { endpoint });
    }

    public Task<List<PushSubscriptionDto>?> ListSubscriptionsAsync()
        => _api.GetAsync<List<PushSubscriptionDto>>("api/push/subscriptions");

    public Task RemoveSubscriptionAsync(int id) => _api.DeleteAsync($"api/push/subscriptions/{id}");

    public Task SendTestAsync() => _api.PostAsync<object>("api/push/test", new { });
}
