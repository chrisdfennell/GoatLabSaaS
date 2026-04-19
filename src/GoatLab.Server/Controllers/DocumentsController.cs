using GoatLab.Server.Services;
using GoatLab.Server.Services.Pdf;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequiresFeature(AppFeature.PdfDocuments)]
public class DocumentsController : ControllerBase
{
    private readonly PdfService _pdf;
    private readonly ITenantContext _tenant;

    public DocumentsController(PdfService pdf, ITenantContext tenant)
    {
        _pdf = pdf;
        _tenant = tenant;
    }

    [HttpGet("goats/{id}/pedigree.pdf")]
    public async Task<IActionResult> Pedigree(int id, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not int tid) return Unauthorized();
        var name = await _pdf.GetTenantNameAsync(tid, cancellationToken);
        var bytes = await _pdf.GeneratePedigreeAsync(id, name, cancellationToken);
        if (bytes is null) return NotFound();
        return File(bytes, "application/pdf", $"pedigree-{id}.pdf");
    }

    [HttpGet("sales/{id}/contract.pdf")]
    public async Task<IActionResult> SalesContract(int id, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not int tid) return Unauthorized();
        var name = await _pdf.GetTenantNameAsync(tid, cancellationToken);
        var bytes = await _pdf.GenerateSalesContractAsync(id, name, cancellationToken);
        if (bytes is null) return NotFound();
        return File(bytes, "application/pdf", $"sales-contract-{id}.pdf");
    }

    [HttpGet("goats/{id}/health-certificate.pdf")]
    public async Task<IActionResult> HealthCertificate(int id, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not int tid) return Unauthorized();
        var name = await _pdf.GetTenantNameAsync(tid, cancellationToken);
        var bytes = await _pdf.GenerateHealthCertificateAsync(id, name, cancellationToken);
        if (bytes is null) return NotFound();
        return File(bytes, "application/pdf", $"health-certificate-{id}.pdf");
    }

    // Comma-separated ids keep this a plain GET so the client can hyperlink to it.
    [HttpGet("qr-sheet.pdf")]
    public async Task<IActionResult> QrSheet([FromQuery] string? ids, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not int tid) return Unauthorized();
        if (string.IsNullOrWhiteSpace(ids))
            return BadRequest(new { error = "Provide at least one goat id in the 'ids' query (comma-separated)." });

        var goatIds = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .Distinct()
            .ToArray();
        if (goatIds.Length == 0)
            return BadRequest(new { error = "No valid goat ids supplied." });

        var name = await _pdf.GetTenantNameAsync(tid, cancellationToken);
        var bytes = await _pdf.GenerateQrSheetAsync(goatIds, name, RequestOrigin(), cancellationToken);
        if (bytes is null) return NotFound();
        return File(bytes, "application/pdf", "qr-ear-tags.pdf");
    }

    private string RequestOrigin()
    {
        var origin = Request.Headers["Origin"].ToString();
        if (!string.IsNullOrEmpty(origin)) return origin;
        return $"{Request.Scheme}://{Request.Host}";
    }
}
