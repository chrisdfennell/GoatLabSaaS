using Microsoft.JSInterop;

namespace GoatLab.Client.Services;

public class OfflineQueueService
{
    private readonly IJSRuntime _js;
    public OfflineQueueService(IJSRuntime js) => _js = js;

    public ValueTask<int> EnqueueAsync(string method, string url, object? body) =>
        _js.InvokeAsync<int>("goatOfflineQueue.enqueue", method, url, body);

    public ValueTask<int> CountAsync() => _js.InvokeAsync<int>("goatOfflineQueue.count");

    public ValueTask<FlushResult> FlushAsync() =>
        _js.InvokeAsync<FlushResult>("goatOfflineQueue.flush");

    public ValueTask ClearAsync() => _js.InvokeVoidAsync("goatOfflineQueue.clear");

    public ValueTask<int> RegisterCountChangedAsync(DotNetObjectReference<object> dotnetRef) =>
        _js.InvokeAsync<int>("goatOfflineQueue.registerCountChanged", dotnetRef);
}

public class FlushResult
{
    public int Flushed { get; set; }
    public int Failed { get; set; }
}

public class OfflineQueuedException : Exception
{
    public OfflineQueuedException(string message) : base(message) { }
}
