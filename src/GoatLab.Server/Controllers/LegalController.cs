using GoatLab.Server.Services.Legal;
using GoatLab.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GoatLab.Server.Controllers;

// Anonymously serves the env-driven values that fill the bracketed
// placeholders in /terms and /privacy. Tiny payload, no PII, cached for an
// hour on the client side via Cache-Control.
[ApiController]
[AllowAnonymous]
[Route("api/legal")]
public class LegalController : ControllerBase
{
    private readonly IOptionsMonitor<LegalOptions> _opts;

    public LegalController(IOptionsMonitor<LegalOptions> opts) => _opts = opts;

    [HttpGet("settings")]
    public ActionResult<LegalSettingsDto> Get()
    {
        var o = _opts.CurrentValue;
        Response.Headers.CacheControl = "public, max-age=3600";
        return new LegalSettingsDto(
            EntityName: NullIfBlank(o.EntityName),
            EntityType: NullIfBlank(o.EntityType),
            State: NullIfBlank(o.State),
            BusinessAddress: NullIfBlank(o.BusinessAddress),
            ContactEmail: NullIfBlank(o.ContactEmail),
            GoverningLawState: NullIfBlank(o.GoverningLawState),
            GoverningLawCounty: NullIfBlank(o.GoverningLawCounty),
            GoverningLawCity: NullIfBlank(o.GoverningLawCity),
            DisputeResolution: NullIfBlank(o.DisputeResolution),
            Approved: o.Approved
        );
    }

    private static string? NullIfBlank(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
