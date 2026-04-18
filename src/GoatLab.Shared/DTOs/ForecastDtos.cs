namespace GoatLab.Shared.DTOs;

// -------- Kidding forecast --------
public record KiddingForecastDto(
    DateTime GeneratedAt,
    int Horizon,
    int Horizon30,
    int Horizon60,
    int Horizon90,
    IReadOnlyList<KiddingForecastItemDto> Upcoming);

public record KiddingForecastItemDto(
    int BreedingRecordId,
    int DoeId,
    string DoeName,
    DateTime EstimatedDueDate,
    int DaysUntilDue);

// -------- Milk forecast --------
public record MilkForecastDto(
    DateTime GeneratedAt,
    int Horizon,
    double TrailingDailyAverage,
    double ProjectedTotal,
    IReadOnlyList<DailyTotalDto> Historical,
    IReadOnlyList<DailyTotalDto> Projected);

// -------- Cash-flow forecast --------
public record CashflowForecastDto(
    DateTime GeneratedAt,
    int Horizon,
    decimal TrailingIncomeDaily,
    decimal TrailingExpenseDaily,
    decimal ProjectedIncome,
    decimal ProjectedExpense,
    decimal ProjectedNet,
    IReadOnlyList<CashflowDayDto> Projected);

public record CashflowDayDto(DateTime Date, decimal Income, decimal Expense, decimal CumulativeNet);
