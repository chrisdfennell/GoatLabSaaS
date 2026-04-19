using GoatLab.Shared.DTOs;

namespace GoatLab.Client.Services;

public class GoatTransfersService
{
    private readonly ApiService _api;
    public GoatTransfersService(ApiService api) => _api = api;

    public Task<InitiateTransferResponse?> InitiateAsync(InitiateTransferRequest req) =>
        _api.PostAsync<InitiateTransferRequest, InitiateTransferResponse>("api/transfers", req);

    public Task<List<GoatTransferSummaryDto>?> ListAsync() =>
        _api.GetAsync<List<GoatTransferSummaryDto>>("api/transfers");

    public Task CancelAsync(int id) =>
        _api.DeleteAsync($"api/transfers/{id}");

    public Task ResendAsync(int id) =>
        _api.PostAsync<object, object>($"api/transfers/{id}/resend", new { });

    // Anon preview uses HttpClient directly so it doesn't pick up the auth cookie hostile path.
    // ApiService.GetAsync goes through the same HttpClient anyway — both are fine.
    public Task<GoatTransferPreviewDto?> PreviewByTokenAsync(string token) =>
        _api.GetAsync<GoatTransferPreviewDto>($"api/transfers/token/{Uri.EscapeDataString(token)}");

    public Task<AcceptTransferResponse?> AcceptAsync(string token, int toTenantId) =>
        _api.PostAsync<AcceptTransferRequest, AcceptTransferResponse>(
            $"api/transfers/token/{Uri.EscapeDataString(token)}/accept",
            new AcceptTransferRequest(toTenantId));

    public Task DeclineAsync(string token, string? reason) =>
        _api.PostAsync<DeclineTransferRequest, object>(
            $"api/transfers/token/{Uri.EscapeDataString(token)}/decline",
            new DeclineTransferRequest(reason));
}
