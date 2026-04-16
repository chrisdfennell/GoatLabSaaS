using Microsoft.JSInterop;

namespace GoatLab.Client.Services;

public class LeafletService : IAsyncDisposable
{
    private readonly IJSRuntime _js;

    public LeafletService(IJSRuntime js) => _js = js;

    public async Task InitAsync<T>(string elementId, double lat, double lng, int zoom, DotNetObjectReference<T>? dotNetRef = null) where T : class
    {
        await _js.InvokeVoidAsync("leafletInterop.init", elementId, lat, lng, zoom, dotNetRef);
    }

    public async Task SetTileLayerAsync(string layerName, string? googleApiKey = null)
    {
        await _js.InvokeVoidAsync("leafletInterop.setTileLayer", layerName, googleApiKey);
    }

    public async Task AddMarkerAsync(int id, double lat, double lng, string name, string type, string? description = null)
    {
        await _js.InvokeVoidAsync("leafletInterop.addMarker", id, lat, lng, name, type, description ?? "");
    }

    public async Task RemoveMarkerAsync(int id)
    {
        await _js.InvokeVoidAsync("leafletInterop.removeMarker", id);
    }

    public async Task AddWaterRadiusAsync(double lat, double lng, string name)
    {
        await _js.InvokeVoidAsync("leafletInterop.addWaterRadius", lat, lng, name);
    }

    public async Task AddPasturePolygonAsync(int id, string geoJson, string name, int condition, double? acreage)
    {
        await _js.InvokeVoidAsync("leafletInterop.addPasturePolygon", id, geoJson, name, condition, acreage);
    }

    public async Task<bool> ToggleMeasureAsync()
    {
        return await _js.InvokeAsync<bool>("leafletInterop.toggleMeasure");
    }

    public async Task FitBoundsAsync()
    {
        await _js.InvokeVoidAsync("leafletInterop.fitBounds");
    }

    public async Task SetViewAsync(double lat, double lng, int zoom)
    {
        await _js.InvokeVoidAsync("leafletInterop.setView", lat, lng, zoom);
    }

    public async Task<string?> ExportGeoJsonAsync()
    {
        return await _js.InvokeAsync<string?>("leafletInterop.exportGeoJson");
    }

    public async Task ClearDrawnAsync()
    {
        await _js.InvokeVoidAsync("leafletInterop.clearDrawn");
    }

    public async ValueTask DisposeAsync()
    {
        try { await _js.InvokeVoidAsync("leafletInterop.dispose"); } catch { /* page unloading */ }
    }
}
