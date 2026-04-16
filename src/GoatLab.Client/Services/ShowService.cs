using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class ShowService
{
    private readonly ApiService _api;
    public ShowService(ApiService api) => _api = api;

    public Task<List<ShowRecord>?> GetShowsAsync(int? goatId = null) =>
        _api.GetAsync<List<ShowRecord>>(goatId.HasValue ? $"api/shows?goatId={goatId}" : "api/shows");
    public Task<ShowRecord?> CreateShowAsync(ShowRecord r) => _api.PostAsync("api/shows", r);
    public Task UpdateShowAsync(ShowRecord r) => _api.PutAsync($"api/shows/{r.Id}", r);
    public Task DeleteShowAsync(int id) => _api.DeleteAsync($"api/shows/{id}");

    public Task<List<LinearAppraisal>?> GetAppraisalsAsync(int? goatId = null) =>
        _api.GetAsync<List<LinearAppraisal>>(goatId.HasValue ? $"api/shows/appraisals?goatId={goatId}" : "api/shows/appraisals");
    public Task<LinearAppraisal?> CreateAppraisalAsync(LinearAppraisal r) => _api.PostAsync("api/shows/appraisals", r);
    public Task UpdateAppraisalAsync(LinearAppraisal r) => _api.PutAsync($"api/shows/appraisals/{r.Id}", r);
    public Task DeleteAppraisalAsync(int id) => _api.DeleteAsync($"api/shows/appraisals/{id}");
}
