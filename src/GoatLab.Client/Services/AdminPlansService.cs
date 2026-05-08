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
        int? MaxPublicListings,
        int? MaxPhotosPerGoat,
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
        int? MaxPublicListings,
        int? MaxPhotosPerGoat,
        bool IsPublic,
        bool IsActive,
        int DisplayOrder,
        List<PlanFeatureDto> Features);

    public async Task<List<AdminPlan>> GetAllAsync()
        => await _http.GetFromJsonAsync<List<AdminPlan>>("api/admin/plans") ?? new();

    public async Task<(AdminPlan? plan, string? error)> CreateAsync(PlanInput input)
    {
        var res = await _http.PostAsJsonAsync("api/admin/plans", input);
        if (!res.IsSuccessStatusCode) return (null, await ReadErrorAsync(res));
        return (await res.Content.ReadFromJsonAsync<AdminPlan>(), null);
    }

    public async Task<(AdminPlan? plan, string? error)> UpdateAsync(int id, PlanInput input)
    {
        var res = await _http.PutAsJsonAsync($"api/admin/plans/{id}", input);
        if (!res.IsSuccessStatusCode) return (null, await ReadErrorAsync(res));
        return (await res.Content.ReadFromJsonAsync<AdminPlan>(), null);
    }

    public async Task<(bool ok, string? error)> DeleteAsync(int id)
    {
        var res = await _http.DeleteAsync($"api/admin/plans/{id}");
        if (res.IsSuccessStatusCode) return (true, null);
        return (false, await ReadErrorAsync(res));
    }

    // Best-effort parse of the server's error shape — { error = "..." } from
    // controllers that return BadRequest, ValidationProblemDetails, or plain
    // text — into something safe to show in a snackbar.
    private static async Task<string> ReadErrorAsync(HttpResponseMessage res)
    {
        var raw = await res.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(raw))
            return $"Save failed ({(int)res.StatusCode} {res.ReasonPhrase}).";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("error", out var err)
                    && err.ValueKind == System.Text.Json.JsonValueKind.String)
                    return err.GetString() ?? raw;
                if (doc.RootElement.TryGetProperty("title", out var title)
                    && title.ValueKind == System.Text.Json.JsonValueKind.String)
                    return title.GetString() ?? raw;
            }
        }
        catch { /* not JSON — fall through to raw text */ }
        return raw.Length > 300 ? raw[..300] + "…" : raw;
    }
}
