using GoatLab.Shared.DTOs;

namespace GoatLab.Client.Services;

public class CoiService
{
    private readonly ApiService _api;
    public CoiService(ApiService api) => _api = api;

    public Task<CoiResultDto?> GetForGoatAsync(int goatId)
        => _api.GetAsync<CoiResultDto>($"api/coi/{goatId}");

    public Task<CoiResultDto?> GetForMateAsync(int sireId, int damId)
        => _api.GetAsync<CoiResultDto>($"api/coi/{sireId}/with/{damId}");
}
