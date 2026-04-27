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

    public Task<EmailLogPageDto?> GetEmailLogAsync(string? recipient = null, string? status = null, int limit = 100)
    {
        var qs = new List<string> { $"limit={limit}" };
        if (!string.IsNullOrWhiteSpace(recipient)) qs.Add($"recipient={Uri.EscapeDataString(recipient)}");
        if (!string.IsNullOrWhiteSpace(status)) qs.Add($"status={Uri.EscapeDataString(status)}");
        return GetJsonAsync<EmailLogPageDto>("api/admin/email-log?" + string.Join("&", qs));
    }

    public Task<AdminSearchResponse?> SearchAsync(string query, int limit = 20)
        => GetJsonAsync<AdminSearchResponse>($"api/admin/search?q={Uri.EscapeDataString(query)}&limit={limit}");

    public Task<StripeSyncResultDto?> SyncTenantAsync(int tenantId)
        => PostJsonAsync<StripeSyncResultDto>($"api/admin/billing/sync/{tenantId}", body: null);

    public Task<StripeReplayResultDto?> ReplayEventAsync(string eventId)
        => PostJsonAsync<StripeReplayResultDto>($"api/admin/billing/replay/{Uri.EscapeDataString(eventId)}", body: null);

    public Task<BulkEmailResultDto?> BulkEmailAsync(BulkEmailRequest req)
        => PostJsonAsync<BulkEmailResultDto>("api/admin/bulk-email", req);

    // Centralized GET that surfaces server errors as HttpRequestException with
    // a useful body excerpt — beats letting GetFromJsonAsync throw an opaque
    // JsonException when the server returned HTML or an error envelope.
    private async Task<T?> GetJsonAsync<T>(string url)
    {
        var resp = await _http.GetAsync(url);
        await EnsureOkOrThrow(resp);
        return await resp.Content.ReadFromJsonAsync<T>();
    }

    private async Task<T?> PostJsonAsync<T>(string url, object? body)
    {
        HttpResponseMessage resp = body is null
            ? await _http.PostAsync(url, content: null)
            : await _http.PostAsJsonAsync(url, body);
        await EnsureOkOrThrow(resp);
        return await resp.Content.ReadFromJsonAsync<T>();
    }

    private static async Task EnsureOkOrThrow(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return;
        var snippet = "";
        try
        {
            var text = await resp.Content.ReadAsStringAsync();
            snippet = text.Length > 240 ? text[..240] + "…" : text;
        }
        catch { /* body already consumed or not readable — fall through */ }
        throw new HttpRequestException(
            $"{(int)resp.StatusCode} {resp.ReasonPhrase}: {snippet}",
            inner: null,
            statusCode: resp.StatusCode);
    }
}
