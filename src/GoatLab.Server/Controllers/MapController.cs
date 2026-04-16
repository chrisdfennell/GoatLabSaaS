using GoatLab.Server.Data;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequiresFeature(AppFeature.Map)]
public class MapController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public MapController(GoatLabDbContext db) => _db = db;

    // --- Map Markers ---

    [HttpGet("markers")]
    public async Task<ActionResult<List<MapMarker>>> GetMarkers([FromQuery] MapMarkerType? type)
    {
        var query = _db.MapMarkers.AsQueryable();
        if (type.HasValue) query = query.Where(m => m.MarkerType == type.Value);
        return await query.OrderBy(m => m.Name).ToListAsync();
    }

    [HttpPost("markers")]
    public async Task<ActionResult<MapMarker>> CreateMarker(MapMarker marker)
    {
        _db.MapMarkers.Add(marker);
        await _db.SaveChangesAsync();
        return Ok(marker);
    }

    [HttpPut("markers/{id}")]
    public async Task<IActionResult> UpdateMarker(int id, MapMarker marker)
    {
        if (id != marker.Id) return BadRequest();
        var existing = await _db.MapMarkers.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = marker.Name;
        existing.MarkerType = marker.MarkerType;
        existing.Latitude = marker.Latitude;
        existing.Longitude = marker.Longitude;
        existing.Description = marker.Description;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("markers/{id}/position")]
    public async Task<IActionResult> SetMarkerPosition(int id, [FromBody] MarkerPosition pos)
    {
        var existing = await _db.MapMarkers.FindAsync(id);
        if (existing is null) return NotFound();
        existing.Latitude = pos.Latitude;
        existing.Longitude = pos.Longitude;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public record MarkerPosition(double Latitude, double Longitude);

    [HttpDelete("markers/{id}")]
    public async Task<IActionResult> DeleteMarker(int id)
    {
        var marker = await _db.MapMarkers.FindAsync(id);
        if (marker is null) return NotFound();
        _db.MapMarkers.Remove(marker);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- Grazing Areas ---

    [HttpGet("grazing-areas")]
    public async Task<ActionResult<List<GrazingArea>>> GetGrazingAreas()
    {
        return await _db.GrazingAreas.OrderBy(g => g.Name).ToListAsync();
    }

    [HttpPost("grazing-areas")]
    public async Task<ActionResult<GrazingArea>> CreateGrazingArea(GrazingArea area)
    {
        area.CreatedAt = DateTime.UtcNow;
        _db.GrazingAreas.Add(area);
        await _db.SaveChangesAsync();
        return Ok(area);
    }

    [HttpPut("grazing-areas/{id}")]
    public async Task<IActionResult> UpdateGrazingArea(int id, GrazingArea area)
    {
        if (id != area.Id) return BadRequest();
        var existing = await _db.GrazingAreas.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = area.Name;
        existing.GeoJson = area.GeoJson;
        existing.Acreage = area.Acreage;
        existing.Notes = area.Notes;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("grazing-areas/{id}")]
    public async Task<IActionResult> DeleteGrazingArea(int id)
    {
        var area = await _db.GrazingAreas.FindAsync(id);
        if (area is null) return NotFound();
        _db.GrazingAreas.Remove(area);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // --- KML Export ---

    [HttpGet("export-kml")]
    public async Task<IActionResult> ExportKml()
    {
        var pastures = await _db.Pastures.Where(p => p.GeoJson != null).ToListAsync();
        var areas = await _db.GrazingAreas.Where(a => a.GeoJson != null).ToListAsync();
        var markers = await _db.MapMarkers.ToListAsync();

        // Simple KML generation
        var kml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <kml xmlns="http://www.opengis.net/kml/2.2">
            <Document>
                <name>GoatLab Farm Map</name>
                {string.Join("\n", markers.Select(m => $"""
                <Placemark>
                    <name>{System.Security.SecurityElement.Escape(m.Name)}</name>
                    <description>{System.Security.SecurityElement.Escape(m.Description ?? "")}</description>
                    <Point><coordinates>{m.Longitude},{m.Latitude},0</coordinates></Point>
                </Placemark>
                """))}
            </Document>
            </kml>
            """;

        return File(System.Text.Encoding.UTF8.GetBytes(kml), "application/vnd.google-earth.kml+xml", "goatlab-farm.kml");
    }
}
