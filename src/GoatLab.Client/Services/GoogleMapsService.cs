using Microsoft.JSInterop;

namespace GoatLab.Client.Services;

public class GoogleMapsService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    public GoogleMapsService(IJSRuntime js) => _js = js;

    public Task<bool> InitAsync<T>(string elementId, string apiKey, double lat, double lng, int zoom, DotNetObjectReference<T> dotNetRef) where T : class
        => _js.InvokeAsync<bool>("goatMaps.init", elementId, apiKey, lat, lng, zoom, dotNetRef).AsTask();

    public Task SetMapTypeAsync(string type) => _js.InvokeVoidAsync("goatMaps.setMapType", type).AsTask();

    public Task AddMarkerAsync(int id, double lat, double lng, string name, string type, string? description = null)
        => _js.InvokeVoidAsync("goatMaps.addMarker", id, lat, lng, name, type, description ?? "").AsTask();

    public Task RemoveMarkerAsync(int id) => _js.InvokeVoidAsync("goatMaps.removeMarker", id).AsTask();

    public Task AddWaterRadiusAsync(int id, double lat, double lng, string name, double radiusMeters = 100)
        => _js.InvokeVoidAsync("goatMaps.addWaterRadius", id, lat, lng, name, radiusMeters).AsTask();

    public Task AddBarnAsync(int id, double lat, double lng, string name, object pens)
        => _js.InvokeVoidAsync("goatMaps.addBarn", id, lat, lng, name, pens).AsTask();

    public Task RemoveBarnAsync(int id) => _js.InvokeVoidAsync("goatMaps.removeBarn", id).AsTask();

    public Task AddPasturePolygonAsync(int id, string geoJson, string name, int condition, double? acreage,
                                       bool isActiveRotation = false, int? daysSinceGrazed = null)
        => _js.InvokeVoidAsync("goatMaps.addPasturePolygon", id, geoJson, name, condition, acreage,
                               isActiveRotation, daysSinceGrazed).AsTask();

    public Task RemovePasturePolygonAsync(int id) => _js.InvokeVoidAsync("goatMaps.removePasturePolygon", id).AsTask();

    public Task SetLayerVisibilityAsync(string layer, bool visible)
        => _js.InvokeVoidAsync("goatMaps.setLayerVisibility", layer, visible).AsTask();

    public Task<bool> ToggleMeasureAsync() => _js.InvokeAsync<bool>("goatMaps.toggleMeasure").AsTask();

    public Task FitBoundsAsync() => _js.InvokeVoidAsync("goatMaps.fitBounds").AsTask();

    public Task SetViewAsync(double lat, double lng, int? zoom = null)
        => _js.InvokeVoidAsync("goatMaps.setView", lat, lng, zoom).AsTask();

    public Task ClearDrawnAsync() => _js.InvokeVoidAsync("goatMaps.clearDrawn").AsTask();

    public async ValueTask DisposeAsync()
    {
        try { await _js.InvokeVoidAsync("goatMaps.dispose"); } catch { }
    }
}
