namespace BatoBuzz.Domain.Enums;

/// <summary>
/// Lifecycle states for sales invoices.
/// </summary>
public enum InvoiceStatus
{
    Draft = 1,
    Issued = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Overdue = 5,
    Cancelled = 6,
    CreditNoteIssued = 7
}
