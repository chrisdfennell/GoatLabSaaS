using Microsoft.JSInterop;

namespace GoatLab.Client.Services;

public class VoiceService
{
    private readonly IJSRuntime _js;
    public VoiceService(IJSRuntime js) => _js = js;

    public Task<bool> IsSupportedAsync() => _js.InvokeAsync<bool>("goatVoice.isSupported").AsTask();

    public Task<bool> StartAsync<T>(DotNetObjectReference<T> dotNetRef) where T : class
        => _js.InvokeAsync<bool>("goatVoice.start", dotNetRef).AsTask();

    public Task StopAsync() => _js.InvokeVoidAsync("goatVoice.stop").AsTask();

    public Task<double?> ParseNumberAsync(string text) => _js.InvokeAsync<double?>("goatVoice.parseNumber", text).AsTask();
}
