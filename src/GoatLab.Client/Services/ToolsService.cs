using System.Net.Http.Json;
using GoatLab.Shared.DTOs;

namespace GoatLab.Client.Services;

public class ToolsService
{
    private readonly ApiService _api;
    public ToolsService(ApiService api) => _api = api;

    public Task<List<ActivityItem>?> GetActivityAsync(int count = 20) => _api.GetAsync<List<ActivityItem>>($"api/tools/activity?count={count}");

    public Task<byte[]> BackupDatabaseAsync() => PostForBytesAsync("api/tools/backup/database");
    public Task<byte[]> BackupMediaAsync() => PostForBytesAsync("api/tools/backup/media");

    // Surface the actual server response on failure so the UI can show "403"
    // (not super-admin), "no media to back up", a SQL Server message, etc.,
    // instead of a catch-all "Backup failed."
    private async Task<byte[]> PostForBytesAsync(string url)
    {
        var resp = await _api.Http.PostAsync(url, null);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            // Trim oversized bodies so the snackbar isn't a wall of HTML.
            if (body.Length > 400) body = body[..400] + "…";
            throw new BackupFailedException((int)resp.StatusCode, resp.ReasonPhrase ?? "", body);
        }
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

public class ActivityItem
{
    public string Type { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class BackupFailedException : Exception
{
    public int StatusCode { get; }
    public string Reason { get; }
    public string Body { get; }

    public BackupFailedException(int statusCode, string reason, string body)
        : base(BuildMessage(statusCode, reason, body))
    {
        StatusCode = statusCode;
        Reason = reason;
        Body = body;
    }

    private static string BuildMessage(int code, string reason, string body) => code switch
    {
        401 => "You're signed out — sign in again and retry.",
        403 => "Backups are super-admin only.",
        404 => string.IsNullOrWhiteSpace(body) ? "Nothing to back up." : body,
        _   => string.IsNullOrWhiteSpace(body)
                ? $"Backup failed ({code} {reason})."
                : $"Backup failed ({code}): {body}",
    };
}
