using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using System.Net.Http.Json;

namespace GoatLab.Client.Services;

public class GoatService
{
    private readonly ApiService _api;
    public GoatService(ApiService api) => _api = api;

    public Task<List<Goat>?> GetAllAsync(GoatStatus? status = null, string? search = null, bool includeExternal = false)
    {
        var url = "api/goats";
        var qs = new List<string>();
        if (status.HasValue) qs.Add($"status={status}");
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        if (includeExternal) qs.Add("includeExternal=true");
        if (qs.Count > 0) url += "?" + string.Join("&", qs);
        return _api.GetAsync<List<Goat>>(url);
    }

    public Task<Goat?> GetAsync(int id) => _api.GetAsync<Goat>($"api/goats/{id}");
    public Task<Goat?> GetPedigreeAsync(int id) => _api.GetAsync<Goat>($"api/goats/{id}/pedigree");
    public Task<Goat?> CreateAsync(Goat goat) => _api.PostAsync("api/goats", goat);
    public Task UpdateAsync(Goat goat) => _api.PutAsync($"api/goats/{goat.Id}", goat);
    public Task DeleteAsync(int id) => _api.DeleteAsync($"api/goats/{id}");
    public Task<HerdStats?> GetStatsAsync() => _api.GetAsync<HerdStats>("api/goats/stats");
    public Task<List<TimelineEntryDto>?> GetTimelineAsync(int id) =>
        _api.GetAsync<List<TimelineEntryDto>>($"api/goats/{id}/timeline");

    public async Task<GoatPhoto?> UploadPhotoAsync(int goatId, Stream fileStream, string fileName, string? caption = null, bool isPrimary = false)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        content.Add(streamContent, "file", fileName);
        if (!string.IsNullOrEmpty(caption))
            content.Add(new StringContent(caption), "caption");
        content.Add(new StringContent(isPrimary.ToString()), "isPrimary");

        return await _api.PostFormAsync<GoatPhoto>($"api/goats/{goatId}/photos", content);
    }

    public Task DeletePhotoAsync(int goatId, int photoId) =>
        _api.DeleteAsync($"api/goats/{goatId}/photos/{photoId}");

    public async Task<GoatDocument?> UploadDocumentAsync(int goatId, Stream fileStream, string fileName,
                                                        string title, string? documentType = null)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        content.Add(new StringContent(title), "title");
        if (!string.IsNullOrEmpty(documentType))
            content.Add(new StringContent(documentType), "documentType");
        return await _api.PostFormAsync<GoatDocument>($"api/goats/{goatId}/documents", content);
    }

    public Task DeleteDocumentAsync(int goatId, int docId) =>
        _api.DeleteAsync($"api/goats/{goatId}/documents/{docId}");
}

public class HerdStats
{
    public int Total { get; set; }
    public int Sick { get; set; }
    public int AtVet { get; set; }
    public int Pregnant { get; set; }
    public int Bucks { get; set; }
    public int Does { get; set; }
}
