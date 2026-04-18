using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class MilkService
{
    private readonly ApiService _api;
    public MilkService(ApiService api) => _api = api;

    public Task<List<MilkLog>?> GetAllAsync(int? goatId = null, DateTime? from = null, DateTime? to = null)
    {
        var qs = new List<string>();
        if (goatId.HasValue) qs.Add($"goatId={goatId}");
        if (from.HasValue) qs.Add($"from={from.Value:yyyy-MM-dd}");
        if (to.HasValue) qs.Add($"to={to.Value:yyyy-MM-dd}");
        var url = "api/milk" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return _api.GetAsync<List<MilkLog>>(url);
    }

    public Task<MilkLog?> CreateAsync(MilkLog log, bool overrideWithdrawal = false) =>
        _api.PostAsync(overrideWithdrawal ? "api/milk?overrideWithdrawal=true" : "api/milk", log);
    public Task UpdateAsync(MilkLog log) => _api.PutAsync($"api/milk/{log.Id}", log);
    public Task DeleteAsync(int id) => _api.DeleteAsync($"api/milk/{id}");
    public Task<List<MilkTrend>?> GetTrendsAsync(int? goatId = null, int days = 30) =>
        _api.GetAsync<List<MilkTrend>>($"api/milk/trends?days={days}" + (goatId.HasValue ? $"&goatId={goatId}" : ""));

    // Lactations
    public Task<List<LactationSummary>?> GetLactationsAsync(int? goatId = null) =>
        _api.GetAsync<List<LactationSummary>>(goatId.HasValue ? $"api/milk/lactations?goatId={goatId}" : "api/milk/lactations");
    public Task<LactationSummary?> GetLactationAsync(int id) =>
        _api.GetAsync<LactationSummary>($"api/milk/lactations/{id}");
    public Task<Lactation?> CreateLactationAsync(Lactation l) => _api.PostAsync("api/milk/lactations", l);
    public Task UpdateLactationAsync(Lactation l) => _api.PutAsync($"api/milk/lactations/{l.Id}", l);
    public Task DeleteLactationAsync(int id) => _api.DeleteAsync($"api/milk/lactations/{id}");

    // Test days
    public Task<MilkTestDay?> CreateTestDayAsync(MilkTestDay t) => _api.PostAsync("api/milk/testdays", t);
    public Task UpdateTestDayAsync(MilkTestDay t) => _api.PutAsync($"api/milk/testdays/{t.Id}", t);
    public Task DeleteTestDayAsync(int id) => _api.DeleteAsync($"api/milk/testdays/{id}");
}

public class MilkTrend
{
    public DateTime Date { get; set; }
    public double TotalLbs { get; set; }
    public int Entries { get; set; }
}

public class LactationSummary
{
    public int Id { get; set; }
    public int GoatId { get; set; }
    public string? GoatName { get; set; }
    public int LactationNumber { get; set; }
    public DateTime FreshenDate { get; set; }
    public DateTime? DryOffDate { get; set; }
    public int? KiddingRecordId { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public int DaysInMilk { get; set; }
    public double TotalMilkLbs { get; set; }
    public double AvgDailyLbs { get; set; }
    public double PeakLbs { get; set; }
    public int PeakDim { get; set; }
    public double? Projected305 { get; set; }
    public int DaysWithData { get; set; }
    public List<LactationTestDay> TestDays { get; set; } = new();
}

public class LactationTestDay
{
    public int Id { get; set; }
    public DateTime TestDate { get; set; }
    public double? AmLbs { get; set; }
    public double? PmLbs { get; set; }
    public double TotalLbs { get; set; }
    public decimal? ButterfatPercent { get; set; }
    public decimal? ProteinPercent { get; set; }
    public int? SomaticCellCount { get; set; }
    public string? Notes { get; set; }
    public int Dim { get; set; }
}
