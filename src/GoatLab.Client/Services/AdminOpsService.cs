using System.Net.Http.Json;
using GoatLab.Shared.DTOs;

namespace GoatLab.Client.Services;

// Single client wrapper for the new super-admin platform-ops endpoints:
// email-log, cross-tenant search, Stripe drift repair, Stripe webhook replay,
// and bulk email. Kept together because each is small and they share a
// "super-admin tools" mental category.
public class AdminOpsService
{
    private readonly HttpClient _http;
    public AdminOpsService(HttpClient http) => _http = http;

    public async Task<EmailLogPageDto?> GetEmailLogAsync(string? recipient = null, string? status = null, int limit = 100)
    {
        var qs = new List<string> { $"limit={limit}" };
        if (!string.IsNullOrWhiteSpace(recipient)) qs.Add($"recipient={Uri.EscapeDataString(recipient)}");
        if (!string.IsNullOrWhiteSpace(status)) qs.Add($"status={Uri.EscapeDataString(status)}");
        return await _http.GetFromJsonAsync<EmailLogPageDto>("api/admin/email-log?" + string.Join("&", qs));
    }

    public async Task<AdminSearchResponse?> SearchAsync(string query, int limit = 20)
        => await _http.GetFromJsonAsync<AdminSearchResponse>(
            $"api/admin/search?q={Uri.EscapeDataString(query)}&limit={limit}");

    public async Task<StripeSyncResultDto?> SyncTenantAsync(int tenantId)
    {
        var resp = await _http.PostAsync($"api/admin/billing/sync/{tenantId}", content: null);
        return await resp.Content.ReadFromJsonAsync<StripeSyncResultDto>();
    }

    public async Task<StripeReplayResultDto?> ReplayEventAsync(string eventId)
    {
        var resp = await _http.PostAsync($"api/admin/billing/replay/{Uri.EscapeDataString(eventId)}", content: null);
        return await resp.Content.ReadFromJsonAsync<StripeReplayResultDto>();
    }

    public async Task<BulkEmailResultDto?> BulkEmailAsync(BulkEmailRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/admin/bulk-email", req);
        return await resp.Content.ReadFromJsonAsync<BulkEmailResultDto>();
    }
}
