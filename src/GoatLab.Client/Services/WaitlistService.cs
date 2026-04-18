using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class WaitlistService
{
    private readonly ApiService _api;
    public WaitlistService(ApiService api) => _api = api;

    public Task<List<WaitlistEntry>?> GetAllAsync(WaitlistStatus? status = null)
    {
        var url = status.HasValue ? $"api/waitlist?status={status}" : "api/waitlist";
        return _api.GetAsync<List<WaitlistEntry>>(url);
    }

    public Task<WaitlistEntry?> GetAsync(int id) => _api.GetAsync<WaitlistEntry>($"api/waitlist/{id}");
    public Task<WaitlistEntry?> CreateAsync(WaitlistEntry entry) => _api.PostAsync("api/waitlist", entry);
    public Task UpdateAsync(WaitlistEntry entry) => _api.PutAsync($"api/waitlist/{entry.Id}", entry);
    public Task DeleteAsync(int id) => _api.DeleteAsync($"api/waitlist/{id}");

    public Task<Sale?> FulfillAsync(int id, int goatId, decimal saleAmount, string? description = null)
        => _api.PostAsync<object, Sale>($"api/waitlist/{id}/fulfill", new { goatId, saleAmount, description });

    public Task OfferAsync(int id) => _api.PostAsync($"api/waitlist/{id}/offer", new { }, noReturn: true);
    public Task CancelAsync(int id, string? reason) => _api.PostAsync($"api/waitlist/{id}/cancel", new { reason }, noReturn: true);
}
