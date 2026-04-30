using System.Net.Http.Json;
using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class TeamService
{
    private readonly HttpClient _http;
    public TeamService(HttpClient http) => _http = http;

    public record Member(string UserId, string Email, string DisplayName, TenantRole Role, DateTime JoinedAt);
    public record Invite(int Id, string Email, TenantRole Role, DateTime CreatedAt, DateTime ExpiresAt);
    public record TeamSnapshot(List<Member> Members, List<Invite> PendingInvites, int? MaxUsers);

    public async Task<TeamSnapshot?> GetAsync()
        => await _http.GetFromJsonAsync<TeamSnapshot>("api/team");

    public async Task<(bool ok, string? error)> CreateInviteAsync(string email, TenantRole role)
    {
        var res = await _http.PostAsJsonAsync("api/team/invites", new { Email = email, Role = role });
        if (res.IsSuccessStatusCode) return (true, null);
        var body = await res.Content.ReadAsStringAsync();
        return (false, body);
    }

    public async Task<bool> RevokeInviteAsync(int id)
    {
        var res = await _http.DeleteAsync($"api/team/invites/{id}");
        return res.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveMemberAsync(string userId)
    {
        var res = await _http.DeleteAsync($"api/team/members/{userId}");
        return res.IsSuccessStatusCode;
    }

    public async Task<(bool ok, string? error)> ChangeRoleAsync(string userId, TenantRole role)
    {
        var res = await _http.PutAsJsonAsync($"api/team/members/{userId}/role", new { Role = role });
        if (res.IsSuccessStatusCode) return (true, null);
        return (false, await res.Content.ReadAsStringAsync());
    }

    public async Task<(bool ok, string? error, int? tenantId, int statusCode)> AcceptInviteAsync(string token)
    {
        var res = await _http.PostAsJsonAsync("api/team/invites/accept", new { Token = token });
        if (!res.IsSuccessStatusCode)
        {
            // ASP.NET's default 401 returns an empty body — surfacing that as
            // an empty error string to the page would render a blank alert
            // box. Caller now also gets the status code so it can route a 401
            // back to "Sign in to accept" instead of guessing.
            var raw = await res.Content.ReadAsStringAsync();
            var msg = string.IsNullOrWhiteSpace(raw)
                ? $"Server returned {(int)res.StatusCode} {res.ReasonPhrase}."
                : raw;
            return (false, msg, null, (int)res.StatusCode);
        }
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        return (true, null, body?.GetValueOrDefault("tenantId"), 200);
    }
}
