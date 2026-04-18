namespace GoatLab.Shared.DTOs;

// Report window. From/To are inclusive calendar dates (UTC).
public record ReportWindowDto(DateTime From, DateTime To);

// -------- P&L --------
public record PnlReportDto(
    ReportWindowDto Window,
    decimal Income,
    decimal Expenses,
    decimal Net,
    IReadOnlyList<CategoryTotalDto> IncomeByCategory,
    IReadOnlyList<CategoryTotalDto> ExpensesByCategory,
    IReadOnlyList<MonthlyPnlDto> Monthly,
    IReadOnlyList<GoatPnlRowDto> CostPerGoat);

public record CategoryTotalDto(string Category, decimal Total);

public record MonthlyPnlDto(int Year, int Month, decimal Income, decimal Expenses, decimal Net);

public record GoatPnlRowDto(int GoatId, string GoatName, decimal Income, decimal Expenses, decimal Net);

// -------- Milk trends --------
public record MilkTrendsReportDto(
    ReportWindowDto Window,
    double TotalLbs,
    double DailyAverageLbs,
    IReadOnlyList<DailyTotalDto> Daily,
    IReadOnlyList<GoatMilkRowDto> TopProducers);

public record DailyTotalDto(DateTime Date, double Lbs);

public record GoatMilkRowDto(int GoatId, string GoatName, double TotalLbs, double AverageLbs, int DaysRecorded);

// -------- Kidding --------
public record KiddingReportDto(
    ReportWindowDto Window,
    int KiddingCount,
    int KidsBorn,
    int KidsAlive,
    int KidsDied,
    double LiveBirthRate,
    double AverageKidsPerKidding,
    int SingleCount,
    int TwinCount,
    int TripletPlusCount,
    IReadOnlyList<MonthlyKiddingDto> Monthly);

public record MonthlyKiddingDto(int Year, int Month, int Kiddings, int KidsBorn, int KidsAlive);

// -------- Mortality --------
public record MortalityReportDto(
    ReportWindowDto Window,
    int DeceasedCount,
    int ActiveHerdAtStart,
    IReadOnlyList<MonthlyMortalityDto> Monthly,
    IReadOnlyList<MortalityGoatDto> Goats);

public record MonthlyMortalityDto(int Year, int Month, int Count);

public record MortalityGoatDto(int GoatId, string GoatName, DateTime ChangedAt);

// -------- Parasite / FAMACHA --------
public record ParasiteReportDto(
    ReportWindowDto Window,
    int ScoreCount,
    double AverageScore,
    int DangerZoneCount,
    double DangerZonePercent,
    IReadOnlyList<MonthlyFamachaDto> Monthly,
    IReadOnlyList<GoatFamachaDto> WorstGoats);

public record MonthlyFamachaDto(int Year, int Month, double AverageScore, int DangerZoneCount);

public record GoatFamachaDto(int GoatId, string GoatName, double AverageScore, int LatestScore, DateTime LatestDate);

// -------- Health spend --------
public record HealthSpendReportDto(
    ReportWindowDto Window,
    decimal TotalSpend,
    decimal AveragePerGoat,
    IReadOnlyList<CategoryTotalDto> ByCategory,
    IReadOnlyList<GoatSpendRowDto> ByGoat,
    IReadOnlyList<MonthlyAmountDto> Monthly);

public record GoatSpendRowDto(int GoatId, string GoatName, decimal Total);

public record MonthlyAmountDto(int Year, int Month, decimal Amount);

// -------- Progeny --------
// One row per (parent, role). Dam-only metrics are null for sire rows.
// DaughterMilk fields are populated for both sires and dams.
public record ProgenyReportDto(
    ReportWindowDto Window,
    IReadOnlyList<ProgenyRowDto> Parents);

public record ProgenyRowDto(
    int ParentId,
    string ParentName,
    GoatLab.Shared.Models.Gender ParentGender,
    int OffspringCount,
    int LiveOffspringCount,
    int? KiddingCount,
    int? KidsBorn,
    int? KidsAlive,
    double? LiveBirthRate,
    double? AvgLitterSize,
    double? AvgBirthWeightLbs,
    double? AvgDaughterDailyMilkLbs,
    int DaughtersWithMilkLogs);
