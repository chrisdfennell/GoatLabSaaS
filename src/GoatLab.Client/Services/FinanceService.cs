using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class FinanceService
{
    private readonly ApiService _api;
    public FinanceService(ApiService api) => _api = api;

    public Task<List<Transaction>?> GetAllAsync(TransactionType? type = null, DateTime? from = null, DateTime? to = null)
    {
        var qs = new List<string>();
        if (type.HasValue) qs.Add($"type={type}");
        if (from.HasValue) qs.Add($"from={from.Value:yyyy-MM-dd}");
        if (to.HasValue) qs.Add($"to={to.Value:yyyy-MM-dd}");
        var url = "api/finance" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return _api.GetAsync<List<Transaction>>(url);
    }

    public Task<Transaction?> CreateAsync(Transaction t) => _api.PostAsync("api/finance", t);
    public Task UpdateAsync(Transaction t) => _api.PutAsync($"api/finance/{t.Id}", t);
    public Task DeleteAsync(int id) => _api.DeleteAsync($"api/finance/{id}");
    public Task<FinanceDashboard?> GetDashboardAsync(int months = 12) => _api.GetAsync<FinanceDashboard>($"api/finance/dashboard?months={months}");

    // P&L + analytics
    public Task<GoatPnlReport?> GetGoatPnlAsync(int goatId) => _api.GetAsync<GoatPnlReport>($"api/finance/goat/{goatId}/pnl");
    public Task<List<CategoryTotal>?> GetExpenseBreakdownAsync(int months = 12) =>
        _api.GetAsync<List<CategoryTotal>>($"api/finance/expense-breakdown?months={months}");
    public Task<byte[]> GetTaxExportAsync(int year) => _api.GetBytesAsync($"api/finance/tax-export?year={year}");

    // Harvests
    public Task<List<HarvestRecord>?> GetHarvestsAsync() => _api.GetAsync<List<HarvestRecord>>("api/finance/harvests");
    public Task<HarvestRecord?> CreateHarvestAsync(HarvestRecord h) => _api.PostAsync("api/finance/harvests", h);
    public Task UpdateHarvestAsync(HarvestRecord h) => _api.PutAsync($"api/finance/harvests/{h.Id}", h);
    public Task DeleteHarvestAsync(int id) => _api.DeleteAsync($"api/finance/harvests/{id}");
}

public class FinanceDashboard
{
    public List<MonthlyTotal> Monthly { get; set; } = new();
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
}

public class MonthlyTotal
{
    public int Year { get; set; }
    public int Month { get; set; }
    public TransactionType Type { get; set; }
    public decimal Total { get; set; }
}

public class CategoryTotal
{
    public string Category { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class GoatPnlReport
{
    public int GoatId { get; set; }
    public string GoatName { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal Net { get; set; }
    public List<Transaction> Transactions { get; set; } = new();
    public List<CategoryTotal> ExpensesByCategory { get; set; } = new();
    public double? WeightGain { get; set; }
    public decimal? CostPerLbGain { get; set; }
}
