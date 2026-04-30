using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoatLab.Server.Controllers;

// Anonymous "Favorites + Compare" page. Pure client-side; favorites live in
// localStorage under the key `goatlab.favorites` (set from the heart button
// on /pub/{slug}/{goatId}). This controller just renders the shell.
[AllowAnonymous]
public class ComparePagesController : Controller
{
    [HttpGet("/compare")]
    public IActionResult Index() => View();
}
