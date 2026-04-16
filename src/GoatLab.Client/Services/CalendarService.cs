using System.Net.Http.Json;
using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class CalendarService
{
    private readonly ApiService _api;
    public CalendarService(ApiService api) => _api = api;

    public Task<List<CalendarEvent>?> GetEventsAsync(DateTime? from = null, DateTime? to = null)
    {
        var qs = new List<string>();
        if (from.HasValue) qs.Add($"from={from.Value:yyyy-MM-dd}");
        if (to.HasValue) qs.Add($"to={to.Value:yyyy-MM-dd}");
        var url = "api/calendar/events" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return _api.GetAsync<List<CalendarEvent>>(url);
    }

    public Task<CalendarEvent?> CreateEventAsync(CalendarEvent e) => _api.PostAsync("api/calendar/events", e);
    public Task UpdateEventAsync(CalendarEvent e) => _api.PutAsync($"api/calendar/events/{e.Id}", e);
    public Task DeleteEventAsync(int id) => _api.DeleteAsync($"api/calendar/events/{id}");

    public Task<List<Checklist>?> GetChecklistsAsync() => _api.GetAsync<List<Checklist>>("api/calendar/checklists");
    public Task<Checklist?> CreateChecklistAsync(Checklist c) => _api.PostAsync("api/calendar/checklists", c);
    public Task UpdateChecklistAsync(Checklist c) => _api.PutAsync($"api/calendar/checklists/{c.Id}", c);
    public Task DeleteChecklistAsync(int id) => _api.DeleteAsync($"api/calendar/checklists/{id}");

    public Task<List<ChecklistCompletion>?> GetCompletionsAsync(DateTime date) =>
        _api.GetAsync<List<ChecklistCompletion>>($"api/calendar/completions?date={date:yyyy-MM-dd}");
    public async Task<ChecklistCompletion?> ToggleCompletionAsync(int itemId, DateTime date)
    {
        var resp = await _api.Http.PostAsync($"api/calendar/completions/toggle?checklistItemId={itemId}&date={date:yyyy-MM-dd}", null);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ChecklistCompletion>();
    }

    // Recurring chore expansion + occurrence completion
    public Task<List<ExpandedOccurrence>?> GetExpandedAsync(DateTime from, DateTime to, bool? choresOnly = null)
    {
        var url = $"api/calendar/expanded?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        if (choresOnly.HasValue) url += $"&choresOnly={choresOnly.Value.ToString().ToLowerInvariant()}";
        return _api.GetAsync<List<ExpandedOccurrence>>(url);
    }

    public Task CompleteOccurrenceAsync(int eventId, DateTime occurrenceDate, string? notes = null) =>
        _api.PostAsync($"api/calendar/events/{eventId}/complete", new { OccurrenceDate = occurrenceDate, Notes = notes });

    public Task UncompleteOccurrenceAsync(int eventId, DateTime occurrenceDate) =>
        _api.DeleteAsync($"api/calendar/events/{eventId}/complete?occurrenceDate={occurrenceDate:yyyy-MM-dd}");
}

public class ExpandedOccurrence
{
    public int EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime OccurrenceDate { get; set; }
    public string Period { get; set; } = "AnyTime";
    public bool IsChore { get; set; }
    public string? Color { get; set; }
    public int? GoatId { get; set; }
    public string? GoatName { get; set; }
    public string Recurrence { get; set; } = "None";
    public bool Completed { get; set; }
    public int? CompletionId { get; set; }
}
