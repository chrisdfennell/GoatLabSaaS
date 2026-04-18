using GoatLab.Shared.DTOs;

namespace GoatLab.Client.Services;

public class ForecastService
{
    private readonly ApiService _api;
    public ForecastService(ApiService api) => _api = api;

    public Task<KiddingForecastDto?> GetKiddingAsync(int days = 90)
        => _api.GetAsync<KiddingForecastDto>($"api/forecasts/kidding?days={days}");

    public Task<MilkForecastDto?> GetMilkAsync(int days = 60)
        => _api.GetAsync<MilkForecastDto>($"api/forecasts/milk?days={days}");

    public Task<CashflowForecastDto?> GetCashflowAsync(int days = 90)
        => _api.GetAsync<CashflowForecastDto>($"api/forecasts/cashflow?days={days}");
}
