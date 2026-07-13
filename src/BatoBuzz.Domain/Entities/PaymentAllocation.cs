namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Links a supplier payment to a specific purchase bill.
/// </summary>
public class PaymentAllocation : AuditableEntity
{
    public Guid PaymentId { get; internal set; }
    public Guid PurchaseBillId { get; internal set; }
    public decimal AmountAllocated { get; internal set; }

    // Navigation
    public Payment Payment { get; private set; } = null!;
    public PurchaseBill PurchaseBill { get; private set; } = null!;
}
