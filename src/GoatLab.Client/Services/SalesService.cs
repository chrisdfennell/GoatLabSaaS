using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class SalesService
{
    private readonly ApiService _api;
    public SalesService(ApiService api) => _api = api;

    public Task<List<Sale>?> GetAllAsync(int? goatId = null)
    {
        var url = goatId.HasValue ? $"api/sales?goatId={goatId}" : "api/sales";
        return _api.GetAsync<List<Sale>>(url);
    }
    public Task<Sale?> GetAsync(int id) => _api.GetAsync<Sale>($"api/sales/{id}");
    public Task<Sale?> CreateAsync(Sale s) => _api.PostAsync("api/sales", s);
    public Task UpdateAsync(Sale s) => _api.PutAsync($"api/sales/{s.Id}", s);
    public Task DeleteAsync(int id) => _api.DeleteAsync($"api/sales/{id}");

    // Customers
    public Task<List<Customer>?> GetCustomersAsync(bool? waitingList = null) =>
        _api.GetAsync<List<Customer>>(waitingList.HasValue ? $"api/sales/customers?waitingList={waitingList}" : "api/sales/customers");
    public Task<Customer?> GetCustomerAsync(int id) => _api.GetAsync<Customer>($"api/sales/customers/{id}");
    public Task<Customer?> CreateCustomerAsync(Customer c) => _api.PostAsync("api/sales/customers", c);
    public Task UpdateCustomerAsync(Customer c) => _api.PutAsync($"api/sales/customers/{c.Id}", c);
    public Task DeleteCustomerAsync(int id) => _api.DeleteAsync($"api/sales/customers/{id}");
}
