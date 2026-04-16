using GoatLab.Server.Data;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public SettingsController(GoatLabDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<Dictionary<string, string?>>> GetAll()
    {
        var settings = await _db.AppSettings.ToListAsync();
        return settings.ToDictionary(s => s.Key, s => s.Value);
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<string?>> Get(string key)
    {
        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        return setting is null ? NotFound() : Ok(setting.Value);
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Set(string key, [FromBody] string? value)
    {
        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting is null)
        {
            setting = new AppSetting { Key = key, Value = value };
            _db.AppSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{key}")]
    public async Task<IActionResult> Delete(string key)
    {
        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting is null) return NotFound();
        _db.AppSettings.Remove(setting);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- PIN Access ---

    [HttpPost("verify-pin")]
    public async Task<ActionResult<object>> VerifyPin([FromBody] string pin)
    {
        var stored = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == "pin");
        if (stored is null || string.IsNullOrEmpty(stored.Value))
            return Ok(new { valid = true, message = "No PIN configured" });

        return Ok(new { valid = stored.Value == pin });
    }

    [HttpPut("pin")]
    public async Task<IActionResult> SetPin([FromBody] string pin)
    {
        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == "pin");
        if (setting is null)
        {
            setting = new AppSetting { Key = "pin", Value = pin };
            _db.AppSettings.Add(setting);
        }
        else
        {
            setting.Value = pin;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
