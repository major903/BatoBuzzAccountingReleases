namespace BatoBuzz.Domain.Exceptions;

/// <summary>
/// Thrown when a journal entry does not balance (total debits != total credits).
/// </summary>
public class UnbalancedJournalException : DomainException
{
    public decimal TotalDebits { get; }
    public decimal TotalCredits { get; }
    public decimal Difference { get; }

    public UnbalancedJournalException(decimal totalDebits, decimal totalCredits, decimal difference)
        : base($"Journal entry is unbalanced. Debits: {totalDebits:N2}, Credits: {totalCredits:N2}, Difference: {difference:N2}")
    {
        TotalDebits = totalDebits;
        TotalCredits = totalCredits;
        Difference = difference;
    }
}
