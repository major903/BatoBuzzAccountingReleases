namespace BatoBuzz.Domain.Enums;

/// <summary>
/// Lifecycle states for financial transactions.
/// </summary>
public enum TransactionStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Posted = 4,
    Cancelled = 5,
    Voided = 6,
    Reversed = 7
}
