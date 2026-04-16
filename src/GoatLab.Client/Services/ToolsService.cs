using System.Net.Http.Json;
using GoatLab.Shared.DTOs;

namespace GoatLab.Client.Services;

public class ToolsService
{
    private readonly ApiService _api;
    public ToolsService(ApiService api) => _api = api;

    public Task<AlertsInfo?> GetAlertsAsync() => _api.GetAsync<AlertsInfo>("api/tools/alerts");
    public Task<List<ActivityItem>?> GetActivityAsync(int count = 20) => _api.GetAsync<List<ActivityItem>>($"api/tools/activity?count={count}");
    public async Task<byte[]> BackupDatabaseAsync()
    {
        var resp = await _api.Http.PostAsync("api/tools/backup/database", null);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsByteArrayAsync();
    }

    public async Task<byte[]> BackupMediaAsync()
    {
        var resp = await _api.Http.PostAsync("api/tools/backup/media", null);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsByteArrayAsync();
    }

    public async Task RestoreDatabaseAsync(Stream fileStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        var resp = await _api.Http.PostAsync("api/tools/restore/database", content);
        resp.EnsureSuccessStatusCode();
    }
    public Task<byte[]> ExportGoatsCsvAsync() => _api.GetBytesAsync("api/tools/export/goats");
    public Task<byte[]> GetGoatImportTemplateAsync() => _api.GetBytesAsync("api/tools/import/goats/template");

    public async Task<GoatImportResult?> ImportGoatsAsync(Stream fileStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        var resp = await _api.Http.PostAsync("api/tools/import/goats", content);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<GoatImportResult>();
    }
    public Task<byte[]> ExportMilkCsvAsync() => _api.GetBytesAsync("api/tools/export/milk-logs");
    public Task<byte[]> ExportMedicalCsvAsync() => _api.GetBytesAsync("api/tools/export/medical-records");
    public Task<byte[]> ExportFinancesCsvAsync() => _api.GetBytesAsync("api/tools/export/finances");
}

public class AlertsInfo
{
    public int OverdueMedications { get; set; }
    public int UpcomingDueDates { get; set; }
    public int LowFeedStock { get; set; }
    public int ExpiringMedications { get; set; }
    public int SickGoats { get; set; }
}

public class ActivityItem
{
    public string Type { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
}
