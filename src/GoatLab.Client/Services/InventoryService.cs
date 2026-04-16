using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class InventoryService
{
    private readonly ApiService _api;
    public InventoryService(ApiService api) => _api = api;

    public Task<List<Supplier>?> GetSuppliersAsync() => _api.GetAsync<List<Supplier>>("api/inventory/suppliers");
    public Task<Supplier?> CreateSupplierAsync(Supplier s) => _api.PostAsync("api/inventory/suppliers", s);
    public Task UpdateSupplierAsync(Supplier s) => _api.PutAsync($"api/inventory/suppliers/{s.Id}", s);
    public Task DeleteSupplierAsync(int id) => _api.DeleteAsync($"api/inventory/suppliers/{id}");

    public Task<List<FeedInventory>?> GetFeedAsync() => _api.GetAsync<List<FeedInventory>>("api/inventory/feed");
    public Task<List<FeedInventory>?> GetLowStockAsync() => _api.GetAsync<List<FeedInventory>>("api/inventory/feed/low-stock");
    public Task<FeedInventory?> CreateFeedAsync(FeedInventory f) => _api.PostAsync("api/inventory/feed", f);
    public Task UpdateFeedAsync(FeedInventory f) => _api.PutAsync($"api/inventory/feed/{f.Id}", f);
    public Task DeleteFeedAsync(int id) => _api.DeleteAsync($"api/inventory/feed/{id}");

    // Medicine cabinet
    public Task<List<MedicineCabinetItem>?> GetMedicineAsync() =>
        _api.GetAsync<List<MedicineCabinetItem>>("api/inventory/medicine");
    public Task<MedicineCabinetItem?> CreateMedicineAsync(MedicineCabinetItem m) =>
        _api.PostAsync("api/inventory/medicine", m);
    public Task UpdateMedicineAsync(MedicineCabinetItem m) =>
        _api.PutAsync($"api/inventory/medicine/{m.Id}", m);
    public Task DeleteMedicineAsync(int id) => _api.DeleteAsync($"api/inventory/medicine/{id}");

    // Unified expiring-soon feed
    public Task<List<ExpiringItem>?> GetExpiringAsync(int days = 30) =>
        _api.GetAsync<List<ExpiringItem>>($"api/inventory/expiring?days={days}");
}

public class ExpiringItem
{
    public string Kind { get; set; } = string.Empty; // "medicine" | "feed"
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string? Unit { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public bool Expired { get; set; }
}
