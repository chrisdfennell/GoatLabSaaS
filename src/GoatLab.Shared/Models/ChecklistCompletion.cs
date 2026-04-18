namespace GoatLab.Shared.Models;

/// <summary>Tracks daily completion of checklist items</summary>
public class ChecklistCompletion : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int ChecklistItemId { get; set; }
    public ChecklistItem? ChecklistItem { get; set; }

    public DateTime Date { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
}
