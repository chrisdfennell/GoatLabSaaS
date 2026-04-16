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
}

public record GoogleMapsKeyResponse(string ApiKey);
