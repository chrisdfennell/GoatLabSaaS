using GoatLab.Shared.DTOs;

namespace GoatLab.Client.Services;

public class MateRecommendationsService
{
    private readonly ApiService _api;
    public MateRecommendationsService(ApiService api) => _api = api;

    public Task<List<MateRecommendationDto>?> GetForDoeAsync(int doeId, int limit = 10) =>
        _api.GetAsync<List<MateRecommendationDto>>($"api/mate-recommendations/{doeId}?limit={limit}");
}
