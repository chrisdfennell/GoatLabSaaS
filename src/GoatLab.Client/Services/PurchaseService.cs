using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class PurchaseService
{
    private readonly ApiService _api;
    public PurchaseService(ApiService api) => _api = api;

    public Task<List<Purchase>?> GetAllAsync(int? goatId = null) =>
        _api.GetAsync<List<Purchase>>(goatId.HasValue ? $"api/purchases?goatId={goatId}" : "api/purchases");

    public Task<CreatePurchaseResult?> CreateAsync(Purchase purchase, Goat? newGoat = null) =>
        _api.PostAsync<CreatePurchaseRequestDto, CreatePurchaseResult>("api/purchases", new CreatePurchaseRequestDto
        {
            Purchase = purchase,
            NewGoat = newGoat
        });

    public Task UpdateAsync(Purchase p) => _api.PutAsync($"api/purchases/{p.Id}", p);
    public Task DeleteAsync(int id) => _api.DeleteAsync($"api/purchases/{id}");
}

public class CreatePurchaseRequestDto
{
    public Purchase Purchase { get; set; } = new();
    public Goat? NewGoat { get; set; }
}

public class CreatePurchaseResult
{
    public Purchase Purchase { get; set; } = new();
    public Goat? Goat { get; set; }
}
