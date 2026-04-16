using GoatLab.Shared.Models;

namespace GoatLab.Client.Services;

public class CareGuideService
{
    private readonly ApiService _api;
    public CareGuideService(ApiService api) => _api = api;

    public Task<List<CareArticle>?> GetAllAsync(CareArticleCategory? category = null) =>
        _api.GetAsync<List<CareArticle>>(category.HasValue ? $"api/careguide?category={category}" : "api/careguide");
    public Task<CareArticle?> GetAsync(int id) => _api.GetAsync<CareArticle>($"api/careguide/{id}");
    public Task<List<CareArticle>?> SearchAsync(string q) => _api.GetAsync<List<CareArticle>>($"api/careguide/search?q={Uri.EscapeDataString(q)}");
}
