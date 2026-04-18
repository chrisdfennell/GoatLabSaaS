using GoatLab.Server.Services.Plans;
using GoatLab.Server.Services.Reports;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiresFeature(AppFeature.Forecasting)]
public class ForecastsController : ControllerBase
{
    private readonly ForecastService _forecast;

    public ForecastsController(ForecastService forecast) => _forecast = forecast;

    [HttpGet("kidding")]
    public async Task<ActionResult<KiddingForecastDto>> Kidding([FromQuery] int days = 90, CancellationToken ct = default)
        => Ok(await _forecast.GetKiddingForecastAsync(Clamp(days, 7, 180), ct));

    [HttpGet("milk")]
    public async Task<ActionResult<MilkForecastDto>> Milk([FromQuery] int days = 60, CancellationToken ct = default)
        => Ok(await _forecast.GetMilkForecastAsync(Clamp(days, 7, 180), ct));

    [HttpGet("cashflow")]
    public async Task<ActionResult<CashflowForecastDto>> Cashflow([FromQuery] int days = 90, CancellationToken ct = default)
        => Ok(await _forecast.GetCashflowForecastAsync(Clamp(days, 7, 365), ct));

    [HttpGet("feed")]
    public async Task<ActionResult<FeedForecastDto>> Feed([FromQuery] int days = 60, CancellationToken ct = default)
        => Ok(await _forecast.GetFeedForecastAsync(Clamp(days, 7, 180), ct));

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
}
