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
[RequiresFeature(AppFeature.AdvancedReports)]
public class ReportsController : ControllerBase
{
    private readonly ReportsService _reports;

    public ReportsController(ReportsService reports) => _reports = reports;

    // All endpoints accept ?from=YYYY-MM-DD&to=YYYY-MM-DD (inclusive).
    // Default window is the trailing 12 months if either bound is omitted.
    private (DateTime from, DateTime to) Range(DateTime? from, DateTime? to)
    {
        var end = to ?? DateTime.UtcNow.Date;
        var start = from ?? end.AddMonths(-12);
        return (start, end);
    }

    [HttpGet("pnl")]
    public async Task<ActionResult<PnlReportDto>> Pnl([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        return Ok(await _reports.GetPnlAsync(f, t, ct));
    }

    [HttpGet("milk-trends")]
    public async Task<ActionResult<MilkTrendsReportDto>> Milk([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        return Ok(await _reports.GetMilkTrendsAsync(f, t, ct));
    }

    [HttpGet("kidding")]
    public async Task<ActionResult<KiddingReportDto>> Kidding([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        return Ok(await _reports.GetKiddingAsync(f, t, ct));
    }

    [HttpGet("mortality")]
    public async Task<ActionResult<MortalityReportDto>> Mortality([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        return Ok(await _reports.GetMortalityAsync(f, t, ct));
    }

    [HttpGet("parasite")]
    public async Task<ActionResult<ParasiteReportDto>> Parasite([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        return Ok(await _reports.GetParasiteAsync(f, t, ct));
    }

    [HttpGet("health-spend")]
    public async Task<ActionResult<HealthSpendReportDto>> HealthSpend([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        return Ok(await _reports.GetHealthSpendAsync(f, t, ct));
    }
}
