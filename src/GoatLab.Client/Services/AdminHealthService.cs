using System.Net.Http.Json;

namespace GoatLab.Client.Services;

public class AdminHealthService
{
    private readonly HttpClient _http;
    public AdminHealthService(HttpClient http) => _http = http;

    public record Check(string Name, string Status, string? Detail);
    public record RecurringJob(string Id, string Cron, DateTime? LastExecution, DateTime? NextExecution, string? LastJobState);
    public record Report(List<Check> Checks, List<RecurringJob> Jobs, DateTime GeneratedAtUtc);
    public record RunBackupResult(bool Queued, string? JobId, string? Message);

    public async Task<Report?> GetAsync()
        => await _http.GetFromJsonAsync<Report>("api/admin/health");

    // Returns (success, message). On 400 from the server (disabled / misconfigured),
    // unwraps the JSON error body so the snackbar shows the actionable text.
    public async Task<(bool ok, string message)> RunBackupAsync()
    {
        var resp = await _http.PostAsync("api/admin/health/backup/run", null);
        var body = await resp.Content.ReadFromJsonAsync<RunBackupResult>();
        return (resp.IsSuccessStatusCode && (body?.Queued ?? false),
                body?.Message ?? (resp.IsSuccessStatusCode ? "Backup queued." : "Backup request failed."));
    }
}
