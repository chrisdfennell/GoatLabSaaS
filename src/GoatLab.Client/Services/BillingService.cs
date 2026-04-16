using System.Net.Http.Json;

namespace GoatLab.Client.Services;

public class BillingService
{
    private readonly HttpClient _http;

    public BillingService(HttpClient http) => _http = http;

    public record BillingStatus(
        int PlanId,
        string PlanName,
        string PlanSlug,
        int PlanPriceMonthlyCents,
        string? Status,
        DateTime? TrialEndsAt,
        DateTime? CurrentPeriodEnd,
        bool HasStripeCustomer);

    public record PublicPlan(
        int Id,
        string Name,
        string Slug,
        string? Description,
        int PriceMonthlyCents,
        int TrialDays,
        int? MaxGoats,
        int? MaxUsers,
        int DisplayOrder,
        List<PlanFeature> Features);

    public record PlanFeature(string Feature, bool Enabled);

    public record RedirectUrl(string Url);

    public record UsageDto(
        string PlanName,
        int GoatCount,
        int? MaxGoats,
        int UserCount,
        int? MaxUsers);

    public async Task<BillingStatus?> GetStatusAsync()
    {
        var res = await _http.GetAsync("api/billing/status");
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<BillingStatus>();
    }

    public async Task<UsageDto?> GetUsageAsync()
    {
        var res = await _http.GetAsync("api/billing/usage");
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<UsageDto>();
    }

    public async Task<List<PublicPlan>> GetPublicPlansAsync()
        => await _http.GetFromJsonAsync<List<PublicPlan>>("api/plans/public") ?? new();

    public async Task<string?> StartCheckoutAsync(int planId)
    {
        var res = await _http.PostAsJsonAsync("api/billing/checkout", new { PlanId = planId });
        if (!res.IsSuccessStatusCode) return null;
        var body = await res.Content.ReadFromJsonAsync<RedirectUrl>();
        return body?.Url;
    }

    public async Task<string?> OpenPortalAsync()
    {
        var res = await _http.PostAsync("api/billing/portal", content: null);
        if (!res.IsSuccessStatusCode) return null;
        var body = await res.Content.ReadFromJsonAsync<RedirectUrl>();
        return body?.Url;
    }
}
