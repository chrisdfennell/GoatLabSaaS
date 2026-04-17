using Microsoft.JSInterop;

namespace GoatLab.Client.Services;

public class NotificationService
{
    private readonly IJSRuntime _js;

    public NotificationService(IJSRuntime js)
    {
        _js = js;
    }

    public Task<bool> IsSupportedAsync() => _js.InvokeAsync<bool>("goatNotify.isSupported").AsTask();
    public Task<string> GetPermissionAsync() => _js.InvokeAsync<string>("goatNotify.permission").AsTask();
    public Task<string> RequestPermissionAsync() => _js.InvokeAsync<string>("goatNotify.request").AsTask();

    public Task<bool> ShowAsync(string title, string body, string? tag = null, string? url = null)
        => _js.InvokeAsync<bool>("goatNotify.show", title, body, tag ?? "goatlab", url ?? "").AsTask();
}
