using System.Net.Http.Json;

namespace GoatLab.Client.Services;

public class BuyerInquiriesService
{
    private readonly ApiService _api;
    public BuyerInquiriesService(ApiService api) => _api = api;

    public Task<List<InquiryListItem>?> ListAsync() =>
        _api.GetAsync<List<InquiryListItem>>("api/inquiries");

    public Task<int?> UnreadCountAsync() =>
        _api.GetAsync<int?>("api/inquiries/unread-count");

    public Task<InquiryDetailDto?> GetAsync(int id) =>
        _api.GetAsync<InquiryDetailDto>($"api/inquiries/{id}");

    public async Task<bool> ReplyAsync(int id, string message)
    {
        var resp = await _api.Http.PostAsJsonAsync($"api/inquiries/{id}/reply", new { Message = message });
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> CloseAsync(int id)
    {
        var resp = await _api.Http.PostAsync($"api/inquiries/{id}/close", null);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> ReopenAsync(int id)
    {
        var resp = await _api.Http.PostAsync($"api/inquiries/{id}/reopen", null);
        return resp.IsSuccessStatusCode;
    }
}

public record InquiryListItem(
    int Id, int GoatId, string GoatName, string BuyerName, string BuyerEmail, string? BuyerPhone,
    string Status, bool UnreadForSeller, DateTime CreatedAt, DateTime LastMessageAt, int MessageCount);

public record InquiryMessageDto(int Id, bool FromSeller, string Body, DateTime CreatedAt);

public record InquiryDetailDto(
    int Id, int GoatId, string GoatName, string? GoatEarTag,
    string BuyerName, string BuyerEmail, string? BuyerPhone,
    string Status, DateTime CreatedAt, DateTime LastMessageAt,
    IReadOnlyList<InquiryMessageDto> Messages);
