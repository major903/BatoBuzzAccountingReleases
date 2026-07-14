namespace BatoBuzz.Domain.Enums;

/// <summary>
/// Lifecycle states for purchase bills.
/// </summary>
public enum BillStatus
{
    Draft = 1,
    Received = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Overdue = 5,
    Cancelled = 6,
    DebitNoteIssued = 7
}
