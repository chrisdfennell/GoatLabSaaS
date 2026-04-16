using System.Net.Http.Json;
using GoatLab.Shared.DTOs;

namespace GoatLab.Client.Services;

public class OnboardingService
{
    private readonly HttpClient _http;
    public OnboardingService(HttpClient http) => _http = http;

    public Task<OnboardingStatus?> GetStatusAsync()
        => _http.GetFromJsonAsync<OnboardingStatus>("api/onboarding/status");
}
