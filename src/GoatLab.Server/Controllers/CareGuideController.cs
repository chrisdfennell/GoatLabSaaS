using GoatLab.Server.Data;
using GoatLab.Server.Services.Plans;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequiresFeature(AppFeature.CareGuide)]
public class CareGuideController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    public CareGuideController(GoatLabDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<CareArticle>>> GetAll([FromQuery] CareArticleCategory? category)
    {
        var query = _db.CareArticles.AsQueryable();
        if (category.HasValue) query = query.Where(a => a.Category == category.Value);
        return await query.OrderBy(a => a.Category).ThenBy(a => a.SortOrder).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CareArticle>> Get(int id)
    {
        var article = await _db.CareArticles.FindAsync(id);
        return article is null ? NotFound() : article;
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<CareArticle>>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return new List<CareArticle>();

        return await _db.CareArticles
            .Where(a => a.Title.Contains(q) || a.Content.Contains(q) || (a.Summary != null && a.Summary.Contains(q)))
            .OrderBy(a => a.Category).ThenBy(a => a.SortOrder)
            .ToListAsync();
    }

    [HttpGet("categories")]
    public ActionResult<object> GetCategories()
    {
        var categories = Enum.GetValues<CareArticleCategory>()
            .Select(c => new { value = (int)c, name = c.ToString() });
        return Ok(categories);
    }

    [HttpPost]
    public async Task<ActionResult<CareArticle>> Create(CareArticle article)
    {
        _db.CareArticles.Add(article);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = article.Id }, article);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, CareArticle article)
    {
        if (id != article.Id) return BadRequest();
        var existing = await _db.CareArticles.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Title = article.Title;
        existing.Category = article.Category;
        existing.Content = article.Content;
        existing.SortOrder = article.SortOrder;
        existing.Summary = article.Summary;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var article = await _db.CareArticles.FindAsync(id);
        if (article is null) return NotFound();
        _db.CareArticles.Remove(article);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
