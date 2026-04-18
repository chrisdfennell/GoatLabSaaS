using System.ComponentModel.DataAnnotations;

namespace GoatLab.Shared.Models;

// Pre-sale reservation. A customer joins the waitlist with optional breed/sex
// preferences and an optional deposit. When an animal becomes available, the
// owner marks the entry Offered, then Fulfilled — fulfillment creates a Sale
// row so the transaction/finance side picks it up automatically.
public class WaitlistEntry : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [MaxLength(100)]
    public string? BreedPreference { get; set; }

    public Gender? SexPreference { get; set; }

    [MaxLength(100)]
    public string? ColorPreference { get; set; }

    // Earliest/latest due date the buyer would accept an animal.
    public DateTime? MinDueDate { get; set; }
    public DateTime? MaxDueDate { get; set; }

    // Deposit in cents (0 = no deposit). DepositPaid toggles when money is received.
    public int DepositCents { get; set; }
    public bool DepositPaid { get; set; }
    public DateTime? DepositReceivedAt { get; set; }

    // Higher priority jumps ahead in the queue. Owner-controlled.
    public int Priority { get; set; }

    public WaitlistStatus Status { get; set; } = WaitlistStatus.Waiting;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? OfferedAt { get; set; }
    public DateTime? FulfilledAt { get; set; }

    // Back-references populated on fulfillment.
    public int? FulfilledSaleId { get; set; }
    public Sale? FulfilledSale { get; set; }
    public int? FulfilledGoatId { get; set; }
    public Goat? FulfilledGoat { get; set; }

    public DateTime? CancelledAt { get; set; }

    [MaxLength(500)]
    public string? CancelReason { get; set; }
}

public enum WaitlistStatus
{
    Waiting = 0,
    Offered = 1,
    Fulfilled = 2,
    Cancelled = 3,
}
