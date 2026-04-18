using GoatLab.Shared.DTOs;

namespace GoatLab.Client.Services;

public class ReportsService
{
    private readonly ApiService _api;
    public ReportsService(ApiService api) => _api = api;

    private static string Range(DateTime? from, DateTime? to)
    {
        var parts = new List<string>();
        if (from.HasValue) parts.Add($"from={from:yyyy-MM-dd}");
        if (to.HasValue) parts.Add($"to={to:yyyy-MM-dd}");
        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }

    public Task<PnlReportDto?> GetPnlAsync(DateTime? from, DateTime? to)
        => _api.GetAsync<PnlReportDto>($"api/reports/pnl{Range(from, to)}");

    public Task<MilkTrendsReportDto?> GetMilkTrendsAsync(DateTime? from, DateTime? to)
        => _api.GetAsync<MilkTrendsReportDto>($"api/reports/milk-trends{Range(from, to)}");

    public Task<KiddingReportDto?> GetKiddingAsync(DateTime? from, DateTime? to)
        => _api.GetAsync<KiddingReportDto>($"api/reports/kidding{Range(from, to)}");

    public Task<MortalityReportDto?> GetMortalityAsync(DateTime? from, DateTime? to)
        => _api.GetAsync<MortalityReportDto>($"api/reports/mortality{Range(from, to)}");

    public Task<ParasiteReportDto?> GetParasiteAsync(DateTime? from, DateTime? to)
        => _api.GetAsync<ParasiteReportDto>($"api/reports/parasite{Range(from, to)}");

    public Task<HealthSpendReportDto?> GetHealthSpendAsync(DateTime? from, DateTime? to)
        => _api.GetAsync<HealthSpendReportDto>($"api/reports/health-spend{Range(from, to)}");
}
