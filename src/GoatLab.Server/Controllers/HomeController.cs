using GoatLab.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoatLab.Server.Controllers;

// The "/" route is mode-aware:
//   - SaaS:  hand off to the Blazor SPA (Landing.razor renders the marketing
//            page) by serving wwwroot/index.html, exactly as MapFallbackToFile
//            would have. Registering the controller intercepts the fallback,
//            so we re-serve the same file rather than letting it through.
//   - OSS:   render Views/Home/OssWelcome.cshtml — a tiny SSR welcome page
//            with sign-in / register CTAs. The SaaS marketing landing has
//            five marketplace links and a "across every farm" pitch that
//            makes no sense for a self-hoster who already chose to self-host.
[AllowAnonymous]
public sealed class HomeController : Controller
{
    private readonly IAppMode _mode;
    private readonly IWebHostEnvironment _env;

    public HomeController(IAppMode mode, IWebHostEnvironment env)
    {
        _mode = mode;
        _env = env;
    }

    [HttpGet("/")]
    public IActionResult Index()
    {
        if (_mode.IsOss)
        {
            // Authed self-hoster lands straight on the dashboard. Anonymous
            // gets the welcome page so they can sign in or claim super-admin.
            if (User.Identity?.IsAuthenticated == true)
            {
                return Redirect("/dashboard");
            }
            return View("OssWelcome");
        }

        var path = Path.Combine(_env.WebRootPath, "index.html");
        return PhysicalFile(path, "text/html");
    }
}
