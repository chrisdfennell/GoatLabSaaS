using System.Net.Http.Json;
using GoatLab.Shared.DTOs;

namespace GoatLab.Client.Services;

public class AnnouncementsService
{
    private readonly HttpClient _http;
    public AnnouncementsService(HttpClient http) => _http = http;

    public Task<List<ActiveAnnouncement>?> GetActiveAsync() =>
        _http.GetFromJsonAsync<List<ActiveAnnouncement>>("api/announcements/active");

    public async Task DismissAsync(int id)
    {
        var resp = await _http.PostAsync($"api/announcements/{id}/dismiss", null);
        resp.EnsureSuccessStatusCode();
    }
}
