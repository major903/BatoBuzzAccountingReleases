namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Links a receipt to a specific sales invoice, recording how much of the receipt was applied.
/// </summary>
public class ReceiptAllocation : AuditableEntity
{
    public Guid ReceiptId { get; internal set; }
    public Guid SalesInvoiceId { get; internal set; }
    public decimal AmountAllocated { get; internal set; }

    // Navigation
    public Receipt Receipt { get; private set; } = null!;
    public SalesInvoice SalesInvoice { get; private set; } = null!;
}
