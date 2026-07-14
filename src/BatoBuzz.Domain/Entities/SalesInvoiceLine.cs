namespace BatoBuzz.Domain.Entities;

/// <summary>
/// A single line item within a sales invoice.
/// </summary>
public class SalesInvoiceLine : AuditableEntity
{
    public Guid SalesInvoiceId { get; internal set; }
    public Guid? ItemId { get; internal set; }
    public string Description { get; internal set; } = null!;
    public decimal Quantity { get; internal set; }
    public decimal Rate { get; internal set; }
    public decimal DiscountPercent { get; internal set; }
    public decimal DiscountAmount { get; internal set; }
    public decimal NetAmount { get; internal set; }
    public decimal TaxPercent { get; internal set; }
    public decimal TaxAmount { get; internal set; }
    public decimal LineTotal { get; internal set; }
    public Guid? WarehouseId { get; internal set; }

    // Navigation
    public SalesInvoice SalesInvoice { get; private set; } = null!;
    public Item? Item { get; private set; }
}
