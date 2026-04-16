using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class HealthService
{
    private readonly ApiService _api;
    public HealthService(ApiService api) => _api = api;

    // Medical Records
    public Task<List<MedicalRecord>?> GetRecordsAsync(int? goatId = null) =>
        _api.GetAsync<List<MedicalRecord>>(goatId.HasValue ? $"api/health/records?goatId={goatId}" : "api/health/records");
    public Task<MedicalRecord?> CreateRecordAsync(MedicalRecord r) => _api.PostAsync("api/health/records", r);
    public Task UpdateRecordAsync(MedicalRecord r) => _api.PutAsync($"api/health/records/{r.Id}", r);
    public Task DeleteRecordAsync(int id) => _api.DeleteAsync($"api/health/records/{id}");
    public Task<List<MedicalRecord>?> GetOverdueAsync() => _api.GetAsync<List<MedicalRecord>>("api/health/overdue");
    public Task<List<MedicalRecord>?> GetUpcomingAsync(int days = 14) => _api.GetAsync<List<MedicalRecord>>($"api/health/upcoming?days={days}");

    // Medications
    public Task<List<Medication>?> GetMedicationsAsync() => _api.GetAsync<List<Medication>>("api/health/medications");
    public Task<Medication?> CreateMedicationAsync(Medication m) => _api.PostAsync("api/health/medications", m);
    public Task UpdateMedicationAsync(Medication m) => _api.PutAsync($"api/health/medications/{m.Id}", m);
    public Task DeleteMedicationAsync(int id) => _api.DeleteAsync($"api/health/medications/{id}");

    // Medicine Cabinet
    public Task<List<MedicineCabinetItem>?> GetCabinetAsync() => _api.GetAsync<List<MedicineCabinetItem>>("api/health/cabinet");
    public Task<MedicineCabinetItem?> CreateCabinetItemAsync(MedicineCabinetItem i) => _api.PostAsync("api/health/cabinet", i);
    public Task UpdateCabinetItemAsync(MedicineCabinetItem i) => _api.PutAsync($"api/health/cabinet/{i.Id}", i);
    public Task DeleteCabinetItemAsync(int id) => _api.DeleteAsync($"api/health/cabinet/{id}");

    // Weights
    public Task<List<WeightRecord>?> GetWeightsAsync(int goatId) => _api.GetAsync<List<WeightRecord>>($"api/health/weights/{goatId}");
    public Task<WeightRecord?> CreateWeightAsync(WeightRecord w) => _api.PostAsync("api/health/weights", w);
    public Task DeleteWeightAsync(int id) => _api.DeleteAsync($"api/health/weights/{id}");

    // FAMACHA
    public Task<List<FamachaScore>?> GetFamachaAsync(int goatId) => _api.GetAsync<List<FamachaScore>>($"api/health/famacha/{goatId}");
    public Task<FamachaScore?> CreateFamachaAsync(FamachaScore f) => _api.PostAsync("api/health/famacha", f);

    // BCS
    public Task<List<BodyConditionScore>?> GetBcsAsync(int goatId) => _api.GetAsync<List<BodyConditionScore>>($"api/health/bcs/{goatId}");
    public Task<BodyConditionScore?> CreateBcsAsync(BodyConditionScore b) => _api.PostAsync("api/health/bcs", b);

    // Summary
    public Task<HealthSummary?> GetSummaryAsync() => _api.GetAsync<HealthSummary>("api/health/summary");

    // Dashboard
    public Task<HealthDashboardData?> GetDashboardAsync() => _api.GetAsync<HealthDashboardData>("api/health/dashboard");
}

public class HealthDashboardData
{
    public List<DashboardOverdue> OverdueMedical { get; set; } = new();
    public List<DashboardUpcoming> UpcomingMedical { get; set; } = new();
    public List<DashboardFamacha> FamachaAtRisk { get; set; } = new();
    public List<DashboardBcs> BcsConcerns { get; set; } = new();
    public List<DashboardWeightLoss> WeightLoss { get; set; } = new();
    public List<DashboardSick> SickGoats { get; set; } = new();
    public List<DashboardExpiringMed> ExpiringMeds { get; set; } = new();
    public DashboardCounts Counts { get; set; } = new();
}

public class DashboardCounts
{
    public int Overdue { get; set; }
    public int Upcoming { get; set; }
    public int Famacha { get; set; }
    public int Bcs { get; set; }
    public int WeightLoss { get; set; }
    public int Sick { get; set; }
    public int ExpiringMeds { get; set; }
}

public class DashboardOverdue
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int GoatId { get; set; }
    public string GoatName { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysOverdue { get; set; }
}

public class DashboardUpcoming
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int GoatId { get; set; }
    public string GoatName { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysUntil { get; set; }
}

public class DashboardFamacha
{
    public int GoatId { get; set; }
    public string GoatName { get; set; } = string.Empty;
    public int Score { get; set; }
    public DateTime Date { get; set; }
}

public class DashboardBcs
{
    public int GoatId { get; set; }
    public string GoatName { get; set; } = string.Empty;
    public int Score { get; set; }
    public DateTime Date { get; set; }
    public string Concern { get; set; } = string.Empty;
}

public class DashboardWeightLoss
{
    public int GoatId { get; set; }
    public string GoatName { get; set; } = string.Empty;
    public double LatestLbs { get; set; }
    public double PriorLbs { get; set; }
    public double LossPct { get; set; }
    public DateTime Since { get; set; }
}

public class DashboardSick
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class DashboardExpiringMed
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? ExpirationDate { get; set; }
    public double Quantity { get; set; }
    public string? Unit { get; set; }
    public bool Expired { get; set; }
}

public class HealthSummary
{
    public double? AverageFamachaScore { get; set; }
    public double? AverageBodyConditionScore { get; set; }
    public int OverdueRecords { get; set; }
    public int MedicationsExpiringSoon { get; set; }
}
