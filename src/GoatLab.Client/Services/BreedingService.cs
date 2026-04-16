using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class BreedingService
{
    private readonly ApiService _api;
    public BreedingService(ApiService api) => _api = api;

    public Task<List<BreedingRecord>?> GetAllAsync() => _api.GetAsync<List<BreedingRecord>>("api/breeding");
    public Task<BreedingRecord?> GetAsync(int id) => _api.GetAsync<BreedingRecord>($"api/breeding/{id}");
    public Task<BreedingRecord?> CreateAsync(BreedingRecord r) => _api.PostAsync("api/breeding", r);
    public Task UpdateAsync(BreedingRecord r) => _api.PutAsync($"api/breeding/{r.Id}", r);
    public Task DeleteAsync(int id) => _api.DeleteAsync($"api/breeding/{id}");

    // Kidding
    public Task<KiddingRecord?> GetKiddingAsync(int id) => _api.GetAsync<KiddingRecord>($"api/breeding/kidding/{id}");
    public Task<KiddingRecord?> CreateKiddingAsync(int breedingId, KiddingRecord r) =>
        _api.PostAsync($"api/breeding/{breedingId}/kidding", r);
    public Task UpdateKiddingAsync(KiddingRecord r) => _api.PutAsync($"api/breeding/kidding/{r.Id}", r);
    public Task DeleteKiddingAsync(int id) => _api.DeleteAsync($"api/breeding/kidding/{id}");

    // Individual Kids
    public Task<Kid?> AddKidAsync(int kiddingId, Kid k) => _api.PostAsync($"api/breeding/kidding/{kiddingId}/kids", k);
    public Task UpdateKidAsync(Kid k) => _api.PutAsync($"api/breeding/kids/{k.Id}", k);
    public Task DeleteKidAsync(int id) => _api.DeleteAsync($"api/breeding/kids/{id}");
    public Task<Goat?> PromoteKidAsync(int kidId) => _api.PostEmptyAsync<Goat>($"api/breeding/kids/{kidId}/promote");

    // Heat Detection
    public Task<List<HeatDetection>?> GetHeatDetectionsAsync(int? goatId = null) =>
        _api.GetAsync<List<HeatDetection>>(goatId.HasValue ? $"api/breeding/heat?goatId={goatId}" : "api/breeding/heat");
    public Task<HeatDetection?> CreateHeatDetectionAsync(HeatDetection h) => _api.PostAsync("api/breeding/heat", h);

    // Kidding Season
    public Task<KiddingSeason?> GetKiddingSeasonAsync() => _api.GetAsync<KiddingSeason>("api/breeding/kidding-season");
}

public class KiddingSeason
{
    public List<BreedingRecord> UpcomingDueDates { get; set; } = new();
    public List<KiddingRecord> RecentBirths { get; set; } = new();
}
