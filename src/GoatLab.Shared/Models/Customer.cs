using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

public class Customer : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>Whether this customer is on a waiting list</summary>
    public bool IsOnWaitingList { get; set; }

    [MaxLength(500)]
    public string? WaitingListNotes { get; set; }

    public ICollection<Sale> Sales { get; set; } = new List<Sale>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
