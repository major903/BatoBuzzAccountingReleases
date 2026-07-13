using BatoBuzz.Contracts.Common;

namespace BatoBuzz.Contracts.Responses;

public class JournalEntryDto
{
    public Guid Id { get; set; }
    public string EntryNumber { get; set; } = null!;
    public DateTime EntryDate { get; set; }
    public string? EntryDateBs { get; set; }
    public VoucherTypeDto VoucherType { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Narration { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public TransactionStatusDto Status { get; set; }
    public Guid? ReversedJournalEntryId { get; set; }
    public string? ReversalReason { get; set; }
    public List<JournalLineDto> Lines { get; set; } = new();
}

public class JournalLineDto
{
    public Guid Id { get; set; }
    public Guid LedgerId { get; set; }
    public string LedgerName { get; set; } = null!;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Narration { get; set; }
}
