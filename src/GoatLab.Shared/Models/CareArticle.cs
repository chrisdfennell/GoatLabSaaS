using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class CareArticle
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public CareArticleCategory Category { get; set; }

    /// <summary>Markdown content for the article body</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Display order within category</summary>
    public int SortOrder { get; set; }

    [MaxLength(500)]
    public string? Summary { get; set; }

    public bool IsBuiltIn { get; set; } = true;
}
