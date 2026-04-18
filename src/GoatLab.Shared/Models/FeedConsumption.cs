using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

// A record of feed used on a given date. Decrementing FeedInventory.QuantityOnHand
// is the controller's responsibility at write time — we don't project usage back
// from inventory snapshots.
public class FeedConsumption : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int FeedInventoryId { get; set; }
    public FeedInventory? FeedInventory { get; set; }

    public DateTime Date { get; set; }

    /// <summary>Quantity used, in the FeedInventory's own Unit (lbs / bales / bags).</summary>
    public double Quantity { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
