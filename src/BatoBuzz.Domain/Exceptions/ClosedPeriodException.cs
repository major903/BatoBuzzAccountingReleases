namespace BatoBuzz.Domain.Exceptions;

/// <summary>
/// Thrown when attempting to post to a closed accounting period.
/// </summary>
public class ClosedPeriodException : DomainException
{
    public DateTime TransactionDate { get; }
    public DateTime PeriodStart { get; }
    public DateTime PeriodEnd { get; }

    public ClosedPeriodException(DateTime transactionDate, DateTime periodStart, DateTime periodEnd)
        : base($"Cannot post transaction dated {transactionDate:d} to a closed period ({periodStart:d} to {periodEnd:d}).")
    {
        TransactionDate = transactionDate;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
    }
}
