using Microsoft.JSInterop;

namespace GoatLab.Client.Services;

public class NotificationService
{
    private readonly IJSRuntime _js;
    private readonly ToolsService _tools;

    public NotificationService(IJSRuntime js, ToolsService tools)
    {
        _js = js;
        _tools = tools;
    }

    public Task<bool> IsSupportedAsync() => _js.InvokeAsync<bool>("goatNotify.isSupported").AsTask();
    public Task<string> GetPermissionAsync() => _js.InvokeAsync<string>("goatNotify.permission").AsTask();
    public Task<string> RequestPermissionAsync() => _js.InvokeAsync<string>("goatNotify.request").AsTask();

    public Task<bool> ShowAsync(string title, string body, string? tag = null, string? url = null)
        => _js.InvokeAsync<bool>("goatNotify.show", title, body, tag ?? "goatlab", url ?? "").AsTask();

    /// <summary>Check current alerts and fire browser notifications for each actionable item.</summary>
    public async Task FireAlertDigestAsync()
    {
        var perm = await GetPermissionAsync();
        if (perm != "granted") return;

        var alerts = await _tools.GetAlertsAsync();
        if (alerts == null) return;

        if (alerts.OverdueMedications > 0)
        {
            await ShowAsync(
                $"🩺 {alerts.OverdueMedications} overdue medication{(alerts.OverdueMedications == 1 ? "" : "s")}",
                "Tap to view health records.",
                tag: "overdue-meds",
                url: "/health");
        }
        if (alerts.UpcomingDueDates > 0)
        {
            await ShowAsync(
                $"🐐 {alerts.UpcomingDueDates} kidding due in the next 14 days",
                "Get the kidding kit ready.",
                tag: "upcoming-kidding",
                url: "/breeding");
        }
        if (alerts.LowFeedStock > 0)
        {
            await ShowAsync(
                $"🌾 {alerts.LowFeedStock} feed item{(alerts.LowFeedStock == 1 ? "" : "s")} low",
                "Time to restock.",
                tag: "low-feed",
                url: "/inventory");
        }
        if (alerts.ExpiringMedications > 0)
        {
            await ShowAsync(
                $"💊 {alerts.ExpiringMedications} medication{(alerts.ExpiringMedications == 1 ? "" : "s")} expiring soon",
                "Check the cabinet.",
                tag: "expiring-meds",
                url: "/health");
        }
    }
}
