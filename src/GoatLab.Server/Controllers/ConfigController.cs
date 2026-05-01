using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoatLab.Server.Controllers;

/// <summary>
/// Server-provided public-ish configuration for the client. The Google Maps
/// JavaScript key is not a secret (the browser loads it in a script URL) but
/// we gate it behind [Authorize] so scrapers can't trivially harvest it.
/// Restrict the key by HTTP referrer in the Google Cloud console.
/// </summary>
[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private readonly IConfiguration _config;

    public ConfigController(IConfiguration config) => _config = config;

    [HttpGet("google-maps-key")]
    public ActionResult<GoogleMapsKeyResponse> GetGoogleMapsKey()
    {
        var key = _config["GoogleMaps:ApiKey"] ?? string.Empty;
        return new GoogleMapsKeyResponse(key);
    }

    /// <summary>
    /// Public reCAPTCHA v3 site key — anonymous because the login/register
    /// pages need it before the user has a session. The key is safe to expose
    /// (it's loaded into a script tag at runtime); the secret stays on server.
    /// Empty string when reCAPTCHA isn't configured (dev), in which case the
    /// client skips the challenge entirely.
    /// </summary>
    [HttpGet("recaptcha-key")]
    [AllowAnonymous]
    public ActionResult<RecaptchaKeyResponse> GetRecaptchaKey()
    {
        var key = _config["Recaptcha:SiteKey"] ?? string.Empty;
        return new RecaptchaKeyResponse(key);
    }
}

public record GoogleMapsKeyResponse(string ApiKey);
public record RecaptchaKeyResponse(string SiteKey);
