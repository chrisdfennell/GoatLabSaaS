using System.Net.Http.Json;
using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class AdminPlansService
{
    private readonly HttpClient _http;

    public AdminPlansService(HttpClient http) => _http = http;

    public record PlanFeatureDto(AppFeature Feature, bool Enabled);

    public record AdminPlan(
        int Id,
        string Name,
        string Slug,
        string? Description,
        int PriceMonthlyCents,
        string? StripePriceId,
        int TrialDays,
        int? MaxGoats,
        int? MaxUsers,
        bool IsPublic,
        bool IsActive,
        int DisplayOrder,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        int TenantCount,
        List<PlanFeatureDto> Features);

    public record PlanInput(
        string Name,
        string Slug,
        string? Description,
        int PriceMonthlyCents,
        string? StripePriceId,
        int TrialDays,
        int? MaxGoats,
        int? MaxUsers,
        bool IsPublic,
        bool IsActive,
        int DisplayOrder,
        List<PlanFeatureDto> Features);

    public async Task<List<AdminPlan>> GetAllAsync()
        => await _http.GetFromJsonAsync<List<AdminPlan>>("api/admin/plans") ?? new();

    public async Task<AdminPlan?> CreateAsync(PlanInput input)
    {
        var res = await _http.PostAsJsonAsync("api/admin/plans", input);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<AdminPlan>();
    }

    public async Task<AdminPlan?> UpdateAsync(int id, PlanInput input)
    {
        var res = await _http.PutAsJsonAsync($"api/admin/plans/{id}", input);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<AdminPlan>();
    }

    public async Task<(bool ok, string? error)> DeleteAsync(int id)
    {
        var res = await _http.DeleteAsync($"api/admin/plans/{id}");
        if (res.IsSuccessStatusCode) return (true, null);
        var body = await res.Content.ReadAsStringAsync();
        return (false, body);
    }
}
