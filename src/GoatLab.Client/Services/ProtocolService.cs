using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class ProtocolService
{
    private readonly ApiService _api;
    public ProtocolService(ApiService api) => _api = api;

    public Task<List<VaccinationProtocol>?> GetAllAsync() =>
        _api.GetAsync<List<VaccinationProtocol>>("api/protocols");
    public Task<VaccinationProtocol?> GetAsync(int id) =>
        _api.GetAsync<VaccinationProtocol>($"api/protocols/{id}");
    public Task<VaccinationProtocol?> CreateAsync(VaccinationProtocol p) =>
        _api.PostAsync("api/protocols", p);
    public Task UpdateAsync(VaccinationProtocol p) => _api.PutAsync($"api/protocols/{p.Id}", p);
    public Task DeleteAsync(int id) => _api.DeleteAsync($"api/protocols/{id}");

    public Task<ApplyProtocolResult?> ApplyAsync(int protocolId, List<int> goatIds, DateTime? startDate = null) =>
        _api.PostAsync<ApplyProtocolRequestDto, ApplyProtocolResult>(
            $"api/protocols/{protocolId}/apply",
            new ApplyProtocolRequestDto { GoatIds = goatIds, StartDate = startDate });
}

public class ApplyProtocolRequestDto
{
    public List<int> GoatIds { get; set; } = new();
    public DateTime? StartDate { get; set; }
}

public class ApplyProtocolResult
{
    public int ProtocolId { get; set; }
    public int RecordsCreated { get; set; }
    public int GoatsCovered { get; set; }
}
