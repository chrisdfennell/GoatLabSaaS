using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace GoatLab.Server.Services.Security;

public class RecaptchaOptions
{
    public string? SiteKey { get; set; }
    public string? SecretKey { get; set; }

    /// <summary>
    /// reCAPTCHA v3 returns a 0.0–1.0 score where 1.0 is "very likely a human".
    /// 0.5 is Google's recommended default. Tighten to 0.7 for high-risk
    /// endpoints, loosen to 0.3 if you're getting too many false positives.
    /// </summary>
    public double MinimumScore { get; set; } = 0.5;
}

public interface IRecaptchaVerifier
{
    /// <summary>
    /// Verifies a reCAPTCHA v3 token against Google's siteverify endpoint.
    /// Returns true when verification passes (success + matching action +
    /// score above the configured floor) OR when reCAPTCHA isn't configured
    /// (so dev/test environments stay usable without a key).
    /// </summary>
    Task<bool> VerifyAsync(string? token, string action, CancellationToken ct = default);
}

public class RecaptchaVerifier : IRecaptchaVerifier
{
    private readonly HttpClient _http;
    private readonly RecaptchaOptions _opts;
    private readonly ILogger<RecaptchaVerifier> _log;

    public RecaptchaVerifier(HttpClient http, IOptions<RecaptchaOptions> opts, ILogger<RecaptchaVerifier> log)
    {
        _http = http;
        _opts = opts.Value;
        _log = log;
    }

    public async Task<bool> VerifyAsync(string? token, string action, CancellationToken ct = default)
    {
        // Dev mode: when SecretKey isn't set, skip verification so localhost
        // and tests don't require live Google calls. Production deployments
        // must set Recaptcha:SecretKey or every protected endpoint will let
        // unverified requests through — startup logs a warning if missing.
        if (string.IsNullOrEmpty(_opts.SecretKey))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            _log.LogInformation("Recaptcha rejected: missing token (action={Action})", action);
            return false;
        }

        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = _opts.SecretKey!,
                ["response"] = token
            });
            using var resp = await _http.PostAsync(
                "https://www.google.com/recaptcha/api/siteverify", content, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Recaptcha verify HTTP {Status}", (int)resp.StatusCode);
                return false;
            }
            var data = await resp.Content.ReadFromJsonAsync<RecaptchaResponse>(cancellationToken: ct);
            if (data is null || !data.Success)
            {
                _log.LogInformation("Recaptcha rejected: success=false (action={Action}, errors={Errors})",
                    action, string.Join(",", data?.ErrorCodes ?? Array.Empty<string>()));
                return false;
            }
            if (!string.IsNullOrEmpty(data.Action) &&
                !string.Equals(data.Action, action, StringComparison.OrdinalIgnoreCase))
            {
                _log.LogInformation("Recaptcha rejected: action mismatch (got={Got}, expected={Expected})",
                    data.Action, action);
                return false;
            }
            if (data.Score < _opts.MinimumScore)
            {
                _log.LogInformation("Recaptcha rejected: score {Score} below threshold {Threshold} (action={Action})",
                    data.Score, _opts.MinimumScore, action);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            // Network blip to Google shouldn't fail the user request silently
            // — but it also shouldn't bypass verification. Log and reject.
            _log.LogWarning(ex, "Recaptcha verify failed (action={Action})", action);
            return false;
        }
    }

    private record RecaptchaResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("score")] double Score,
        [property: JsonPropertyName("action")] string? Action,
        [property: JsonPropertyName("hostname")] string? Hostname,
        [property: JsonPropertyName("error-codes")] string[]? ErrorCodes);
}
