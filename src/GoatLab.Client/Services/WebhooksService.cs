namespace GoatLab.Client.Services;

public class WebhooksService
{
    private readonly ApiService _api;
    public WebhooksService(ApiService api) => _api = api;

    public record WebhookDto(int Id, string Name, string Url, string Events, bool IsActive,
        DateTime CreatedAt, DateTime UpdatedAt,
        DateTime? LastDeliveredAt, int? LastStatusCode, string? LastError);
    public record CreatedWebhookDto(int Id, string Name, string Url, string Events, bool IsActive,
        DateTime CreatedAt, string Secret);
    public record CreateOrUpdateRequest(string Name, string Url, string Events, bool IsActive);
    public record DeliveryDto(int Id, string EventType, string DeliveryId, int AttemptCount,
        int? StatusCode, string? Error, DateTime CreatedAt, DateTime? DeliveredAt, DateTime? NextRetryAt);

    public Task<List<WebhookDto>?> ListAsync() => _api.GetAsync<List<WebhookDto>>("api/webhooks");
    public Task<WebhookDto?> GetAsync(int id) => _api.GetAsync<WebhookDto>($"api/webhooks/{id}");

    public Task<CreatedWebhookDto?> CreateAsync(CreateOrUpdateRequest req) =>
        _api.PostAsync<CreateOrUpdateRequest, CreatedWebhookDto>("api/webhooks", req);

    public Task UpdateAsync(int id, CreateOrUpdateRequest req) =>
        _api.PutAsync($"api/webhooks/{id}", req);

    public Task DeleteAsync(int id) => _api.DeleteAsync($"api/webhooks/{id}");

    public Task TestAsync(int id) => _api.PostAsync($"api/webhooks/{id}/test", new { }, noReturn: true);

    public Task<List<DeliveryDto>?> DeliveriesAsync(int id) =>
        _api.GetAsync<List<DeliveryDto>>($"api/webhooks/{id}/deliveries");
}
