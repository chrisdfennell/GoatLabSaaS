namespace GoatLab.Client.Services;

public class ApiKeysService
{
    private readonly ApiService _api;
    public ApiKeysService(ApiService api) => _api = api;

    // DTOs mirror the controller records exactly so we can bind straight into them.
    public record ApiKeySummaryDto(int Id, string Name, string Prefix,
        DateTime CreatedAt, DateTime? LastUsedAt, DateTime? ExpiresAt);
    public record CreatedKeyDto(int Id, string Name, string Prefix,
        DateTime CreatedAt, DateTime? ExpiresAt, string PlaintextKey);
    public record CreateRequest(string Name, DateTime? ExpiresAt);

    public Task<List<ApiKeySummaryDto>?> ListAsync() =>
        _api.GetAsync<List<ApiKeySummaryDto>>("api/apikeys");

    public Task<CreatedKeyDto?> CreateAsync(string name, DateTime? expiresAt) =>
        _api.PostAsync<CreateRequest, CreatedKeyDto>("api/apikeys", new CreateRequest(name, expiresAt));

    public Task RevokeAsync(int id) => _api.DeleteAsync($"api/apikeys/{id}");
}
