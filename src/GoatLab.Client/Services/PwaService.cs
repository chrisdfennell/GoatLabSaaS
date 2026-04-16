using Microsoft.JSInterop;

namespace GoatLab.Client.Services;

public class PwaService
{
    private readonly IJSRuntime _js;
    public PwaService(IJSRuntime js) => _js = js;

    public ValueTask<bool> IsOnlineAsync() => _js.InvokeAsync<bool>("goatPwa.isOnline");
    public ValueTask<bool> CanInstallAsync() => _js.InvokeAsync<bool>("goatPwa.canInstall");
    public ValueTask<bool> IsStandaloneAsync() => _js.InvokeAsync<bool>("goatPwa.isStandalone");
    public ValueTask<string> PromptInstallAsync() => _js.InvokeAsync<string>("goatPwa.promptInstall");

    public ValueTask<int> RegisterOnlineChangedAsync(DotNetObjectReference<object> dotnetRef)
        => _js.InvokeAsync<int>("goatPwa.registerOnlineChanged", dotnetRef);

    public ValueTask<int> RegisterInstallAvailableChangedAsync(DotNetObjectReference<object> dotnetRef)
        => _js.InvokeAsync<int>("goatPwa.registerInstallAvailableChanged", dotnetRef);
}
