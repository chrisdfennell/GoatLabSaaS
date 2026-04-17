using GoatLab.Shared.DTOs;

namespace GoatLab.Client.Services;

public class AlertsService
{
    private readonly ApiService _api;
    public AlertsService(ApiService api) => _api = api;

    public Task<List<AlertDto>?> ListAsync(bool includeDismissed = false, int limit = 100)
        => _api.GetAsync<List<AlertDto>>($"api/alerts?includeDismissed={includeDismissed}&limit={limit}");

    public Task<int?> UnreadCountAsync() => _api.GetAsync<int?>("api/alerts/unread-count");

    public Task MarkReadAsync(int id) => _api.PostAsync<object>($"api/alerts/{id}/read", new { });
    public Task DismissAsync(int id) => _api.PostAsync<object>($"api/alerts/{id}/dismiss", new { });
    public Task DismissAllAsync() => _api.PostAsync<object>("api/alerts/dismiss-all", new { });
}
