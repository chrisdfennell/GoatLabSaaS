using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class Checklist : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public TaskPeriod Period { get; set; }

    public ICollection<ChecklistItem> Items { get; set; } = new List<ChecklistItem>();
}

public class ChecklistItem : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int ChecklistId { get; set; }
    public Checklist? Checklist { get; set; }

    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
