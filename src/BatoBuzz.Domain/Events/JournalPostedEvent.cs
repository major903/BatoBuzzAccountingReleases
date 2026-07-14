namespace BatoBuzz.Domain.Events;

/// <summary>
/// Raised when a journal entry is successfully posted.
/// </summary>
public class JournalPostedEvent : DomainEvent
{
    public Guid JournalEntryId { get; }
    public Guid CompanyId { get; }
    public string EntryNumber { get; }
    public DateTime PostingDate { get; }
    public decimal TotalAmount { get; }

    public JournalPostedEvent(Guid journalEntryId, Guid companyId, string entryNumber, DateTime postingDate, decimal totalAmount)
    {
        JournalEntryId = journalEntryId;
        CompanyId = companyId;
        EntryNumber = entryNumber;
        PostingDate = postingDate;
        TotalAmount = totalAmount;
    }
}
