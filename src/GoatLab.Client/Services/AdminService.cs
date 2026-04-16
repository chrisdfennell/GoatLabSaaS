using System.Net;
using System.Net.Http.Json;
using GoatLab.Shared.DTOs;

namespace GoatLab.Client.Services;

/// <summary>
/// Client wrapper around /api/admin/* endpoints. All calls require the signed-in
/// user to carry the super_admin claim; the server enforces with the SuperAdmin
/// authorization policy.
/// </summary>
public class AdminService
{
    private readonly HttpClient _http;
    public AdminService(HttpClient http) => _http = http;

    // Metrics
    public Task<AdminMetrics?> GetMetricsAsync() =>
        _http.GetFromJsonAsync<AdminMetrics>("api/admin/metrics");

    public Task<AdminTimeseries?> GetTimeseriesAsync(int days = 30) =>
        _http.GetFromJsonAsync<AdminTimeseries>($"api/admin/metrics/timeseries?days={days}");

    // Tenants
    public Task<List<AdminTenantRow>?> GetTenantsAsync() =>
        _http.GetFromJsonAsync<List<AdminTenantRow>>("api/admin/tenants");

    public Task<AdminTenantDetail?> GetTenantDetailAsync(int id) =>
        _http.GetFromJsonAsync<AdminTenantDetail>($"api/admin/tenants/{id}");

    public Task RenameTenantAsync(int id, string name) =>
        EnsurePutAsync($"api/admin/tenants/{id}", new AdminRenameTenantRequest(name));

    public Task SuspendTenantAsync(int id, string? reason) =>
        EnsurePostAsync($"api/admin/tenants/{id}/suspend", new AdminSuspendTenantRequest(reason));

    public Task UnsuspendTenantAsync(int id) =>
        EnsurePostAsync($"api/admin/tenants/{id}/unsuspend", new { });

    public Task DeleteTenantAsync(int id) =>
        EnsurePostAsync($"api/admin/tenants/{id}/delete", new { });

    public Task RestoreTenantAsync(int id) =>
        EnsurePostAsync($"api/admin/tenants/{id}/restore", new { });

    public Task SetTenantNotesAsync(int id, string? notes) =>
        EnsurePutAsync($"api/admin/tenants/{id}/notes", new AdminTenantNotesRequest(notes));

    public Task SetTenantTagAsync(int id, string? tag) =>
        EnsurePutAsync($"api/admin/tenants/{id}/tag", new AdminTenantTagRequest(tag));

    public Task SetTenantFlagsAsync(int id, IReadOnlyDictionary<string, bool> flags) =>
        EnsurePutAsync($"api/admin/tenants/{id}/flags", new AdminTenantFlagsRequest(flags));

    // Users
    public Task<List<AdminUserRow>?> GetUsersAsync() =>
        _http.GetFromJsonAsync<List<AdminUserRow>>("api/admin/users");

    public Task<AdminUserDetail?> GetUserDetailAsync(string id) =>
        _http.GetFromJsonAsync<AdminUserDetail>($"api/admin/users/{id}");

    public Task ToggleSuperAdminAsync(string id, bool isSuperAdmin) =>
        EnsurePostAsync($"api/admin/users/{id}/super-admin", new AdminToggleSuperAdminRequest(isSuperAdmin));

    public async Task<AdminResetPasswordResponse?> ResetPasswordAsync(string id)
    {
        var resp = await _http.PostAsync($"api/admin/users/{id}/reset-password", null);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<AdminResetPasswordResponse>();
    }

    public Task LockUserAsync(string id, int? durationHours) =>
        EnsurePostAsync($"api/admin/users/{id}/lock", new AdminLockUserRequest(durationHours));

    public Task UnlockUserAsync(string id) =>
        EnsurePostAsync($"api/admin/users/{id}/unlock", new { });

    public Task ForceSignOutAsync(string id) =>
        EnsurePostAsync($"api/admin/users/{id}/sign-out-everywhere", new { });

    public Task DeleteUserAsync(string id) =>
        EnsurePostAsync($"api/admin/users/{id}/delete", new { });

    public Task RestoreUserAsync(string id) =>
        EnsurePostAsync($"api/admin/users/{id}/restore", new { });

    // Impersonation
    public async Task<ImpersonationState?> GetImpersonationAsync()
    {
        var resp = await _http.GetAsync("api/admin/impersonation");
        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ImpersonationState>();
    }

    public Task StartImpersonationAsync(int tenantId) =>
        EnsurePostAsync($"api/admin/impersonate/{tenantId}", new { });

    public Task ExitImpersonationAsync() =>
        EnsurePostAsync("api/admin/impersonate/exit", new { });

    // Audit
    public Task<AdminAuditPage?> GetAuditAsync(int page = 1, int pageSize = 50) =>
        _http.GetFromJsonAsync<AdminAuditPage>($"api/admin/audit?page={page}&pageSize={pageSize}");

    public Task<byte[]> ExportAuditCsvAsync() =>
        _http.GetByteArrayAsync("api/admin/audit/export");

    // Announcements
    public Task<List<AdminAnnouncementRow>?> GetAnnouncementsAsync() =>
        _http.GetFromJsonAsync<List<AdminAnnouncementRow>>("api/admin/announcements");

    public async Task<AdminAnnouncementRow?> CreateAnnouncementAsync(AdminAnnouncementUpsert req)
    {
        var resp = await _http.PostAsJsonAsync("api/admin/announcements", req);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<AdminAnnouncementRow>();
    }

    public Task UpdateAnnouncementAsync(int id, AdminAnnouncementUpsert req) =>
        EnsurePutAsync($"api/admin/announcements/{id}", req);

    public async Task DeleteAnnouncementAsync(int id)
    {
        var resp = await _http.DeleteAsync($"api/admin/announcements/{id}");
        resp.EnsureSuccessStatusCode();
    }

    // Maintenance
    public Task<AdminMaintenanceStatus?> GetMaintenanceAsync() =>
        _http.GetFromJsonAsync<AdminMaintenanceStatus>("api/admin/maintenance");

    public Task SetMaintenanceAsync(bool enabled) =>
        EnsurePostAsync("api/admin/maintenance", new AdminMaintenanceRequest(enabled));

    // --- helpers ---

    private async Task EnsurePostAsync<T>(string url, T body)
    {
        var resp = await _http.PostAsJsonAsync(url, body);
        resp.EnsureSuccessStatusCode();
    }

    private async Task EnsurePutAsync<T>(string url, T body)
    {
        var resp = await _http.PutAsJsonAsync(url, body);
        resp.EnsureSuccessStatusCode();
    }
}
