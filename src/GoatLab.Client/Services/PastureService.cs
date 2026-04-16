using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class PastureService
{
    private readonly ApiService _api;
    public PastureService(ApiService api) => _api = api;

    public Task<List<Pasture>?> GetAllAsync() => _api.GetAsync<List<Pasture>>("api/pastures");
    public Task<Pasture?> GetAsync(int id) => _api.GetAsync<Pasture>($"api/pastures/{id}");
    public Task<Pasture?> CreateAsync(Pasture p) => _api.PostAsync("api/pastures", p);
    public Task UpdateAsync(Pasture p) => _api.PutAsync($"api/pastures/{p.Id}", p);
    public Task DeleteAsync(int id) => _api.DeleteAsync($"api/pastures/{id}");

    public Task<PastureConditionLog?> CreateConditionAsync(int pastureId, PastureConditionLog log) =>
        _api.PostAsync($"api/pastures/{pastureId}/conditions", log);

    // Map
    public Task<List<MapMarker>?> GetMarkersAsync() => _api.GetAsync<List<MapMarker>>("api/map/markers");
    public Task<MapMarker?> CreateMarkerAsync(MapMarker m) => _api.PostAsync("api/map/markers", m);
    public Task UpdateMarkerAsync(MapMarker m) => _api.PutAsync($"api/map/markers/{m.Id}", m);
    public Task DeleteMarkerAsync(int id) => _api.DeleteAsync($"api/map/markers/{id}");

    public Task SetMarkerPositionAsync(int id, double lat, double lng) =>
        _api.PatchJsonAsync($"api/map/markers/{id}/position", new { Latitude = lat, Longitude = lng });

    public Task<List<PastureRotation>?> GetRotationTimelineAsync() =>
        _api.GetAsync<List<PastureRotation>>("api/pastures/rotation-timeline");
    public Task<PastureRotation?> CreateRotationAsync(int pastureId, PastureRotation r) =>
        _api.PostAsync($"api/pastures/{pastureId}/rotations", r);
    public async Task EndRotationAsync(int rotationId)
    {
        var resp = await _api.Http.PutAsync($"api/pastures/rotations/{rotationId}/end", null);
        resp.EnsureSuccessStatusCode();
    }

    public Task<List<GrazingArea>?> GetGrazingAreasAsync() => _api.GetAsync<List<GrazingArea>>("api/map/grazing-areas");
    public Task<GrazingArea?> CreateGrazingAreaAsync(GrazingArea a) => _api.PostAsync("api/map/grazing-areas", a);
}
