using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class BarnService
{
    private readonly ApiService _api;
    public BarnService(ApiService api) => _api = api;

    public Task<List<Barn>?> GetAllAsync() => _api.GetAsync<List<Barn>>("api/barns");
    public Task<Barn?> GetAsync(int id) => _api.GetAsync<Barn>($"api/barns/{id}");
    public Task<Barn?> CreateAsync(Barn b) => _api.PostAsync("api/barns", b);
    public Task UpdateAsync(Barn b) => _api.PutAsync($"api/barns/{b.Id}", b);
    public Task DeleteAsync(int id) => _api.DeleteAsync($"api/barns/{id}");

    public Task SetPositionAsync(int barnId, double lat, double lng) =>
        _api.PatchJsonAsync($"api/barns/{barnId}/position", new { Latitude = lat, Longitude = lng });

    public Task<List<Pen>?> GetPensAsync(int barnId) => _api.GetAsync<List<Pen>>($"api/barns/{barnId}/pens");
    public Task<Pen?> CreatePenAsync(int barnId, Pen p) => _api.PostAsync($"api/barns/{barnId}/pens", p);
    public Task UpdatePenAsync(Pen p) => _api.PutAsync($"api/barns/pens/{p.Id}", p);
    public Task DeletePenAsync(int penId) => _api.DeleteAsync($"api/barns/pens/{penId}");
}
