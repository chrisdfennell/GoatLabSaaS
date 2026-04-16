using System.Net.Http.Json;

namespace GoatLab.Client.Services;

public class AdminHealthService
{
    private readonly HttpClient _http;
    public AdminHealthService(HttpClient http) => _http = http;

    public record Check(string Name, string Status, string? Detail);
    public record RecurringJob(string Id, string Cron, DateTime? LastExecution, DateTime? NextExecution, string? LastJobState);
    public record Report(List<Check> Checks, List<RecurringJob> Jobs, DateTime GeneratedAtUtc);

    public async Task<Report?> GetAsync()
        => await _http.GetFromJsonAsync<Report>("api/admin/health");
}
