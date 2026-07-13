using BatoBuzz.Domain.Common;
using BatoBuzz.Domain.Enums;
using BatoBuzz.Domain.Events;
using BatoBuzz.Domain.Exceptions;

namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Represents a journal entry (voucher) in the double-entry bookkeeping system.
/// This is an aggregate root that ensures the fundamental accounting equation: Debits = Credits.
/// </summary>
public class JournalEntry : AuditableEntity, IAggregateRoot
{
    public string EntryNumber { get; private set; } = null!;
    public DateTime EntryDate { get; private set; }
    public string? EntryDateBs { get; private set; }
    public VoucherType VoucherType { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public string? Narration { get; private set; }
    public decimal TotalDebit { get; private set; }
    public decimal TotalCredit { get; private set; }
    public TransactionStatus Status { get; private set; } = TransactionStatus.Draft;
    public Guid? ReversedJournalEntryId { get; private set; }
    public string? ReversalReason { get; private set; }
    public JournalEntry? ReversalJournalEntry { get; private set; }
    public DateTime? PostedAt { get; private set; }
    public Guid? PostedByUserId { get; private set; }

    // Navigation
    public Guid? BranchId { get; private set; }
    public Branch? Branch { get; private set; }
    public ICollection<JournalLine> Lines { get; private set; } = new List<JournalLine>();

    private JournalEntry() { }

    public static JournalEntry Create(
        Guid companyId,
        DateTime entryDate,
        VoucherType voucherType,
        string entryNumber,
        Guid createdByUserId,
        string? referenceNumber = null,
        string? narration = null,
        Guid? branchId = null,
        string? entryDateBs = null)
    {
        return new JournalEntry
        {
            CompanyId = companyId,
            EntryDate = entryDate.Date,
            VoucherType = voucherType,
            EntryNumber = entryNumber,
            ReferenceNumber = referenceNumber,
            Narration = narration,
            BranchId = branchId,
            EntryDateBs = entryDateBs,
            CreatedByUserId = createdByUserId
        };
    }

    public void AddLine(Guid ledgerId, decimal debitAmount, decimal creditAmount, string? narration = null, string? costCentre = null, decimal? taxRate = null, string? taxCode = null)
    {
        debitAmount = Money.Round(debitAmount);
        creditAmount = Money.Round(creditAmount);

        if (Status == TransactionStatus.Posted)
            throw new InvalidOperationException("Cannot modify a posted journal entry.");

        if (debitAmount < 0 || creditAmount < 0)
            throw new ArgumentException("Amounts cannot be negative.");

        if (debitAmount > 0 && creditAmount > 0)
            throw new ArgumentException("A journal line cannot have both debit and credit amounts.");

        if (debitAmount == 0 && creditAmount == 0)
            throw new ArgumentException("A journal line must have either a debit or credit amount.");

        var line = new JournalLine
        {
            CompanyId = CompanyId,
            JournalEntryId = Id,
            LedgerId = ledgerId,
            DebitAmount = debitAmount,
            CreditAmount = creditAmount,
            Narration = narration,
            CostCentre = costCentre,
            TaxRate = taxRate,
            TaxCode = taxCode
        };

        Lines.Add(line);
        RecalculateTotals();
    }

    public void RemoveLine(Guid lineId)
    {
        if (Status == TransactionStatus.Posted)
            throw new InvalidOperationException("Cannot modify a posted journal entry.");

        var line = Lines.FirstOrDefault(l => l.Id == lineId);
        if (line != null)
        {
            Lines.Remove(line);
            RecalculateTotals();
        }
    }

    public void Post(Guid postedByUserId)
    {
        if (Status == TransactionStatus.Posted)
            throw new InvalidOperationException("Journal entry is already posted.");

        if (Status == TransactionStatus.Cancelled || Status == TransactionStatus.Voided)
            throw new InvalidOperationException("Cannot post a cancelled or voided journal entry.");

        if (TotalDebit != TotalCredit)
            throw new UnbalancedJournalException(TotalDebit, TotalCredit, Math.Abs(TotalDebit - TotalCredit));

        if (TotalDebit == 0)
            throw new InvalidOperationException("Cannot post a journal entry with zero amounts.");

        Status = TransactionStatus.Posted;
        PostedAt = DateTime.UtcNow;
        PostedByUserId = postedByUserId;

        // Update ledger balances
        foreach (var line in Lines)
        {
            line.Ledger.UpdateBalance(line.DebitAmount, line.CreditAmount);
        }

        // Raise domain event
        // DomainEvents.Raise(new JournalPostedEvent(Id, CompanyId, EntryNumber, EntryDate, TotalDebit));
    }

    public void MarkReversed(Guid reversalJournalEntryId, Guid reversedByUserId, string reason)
    {
        if (Status != TransactionStatus.Posted)
            throw new InvalidOperationException("Only posted journal entries can be reversed.");
        if (reversalJournalEntryId == Guid.Empty)
            throw new ArgumentException("Reversal journal entry ID is required.", nameof(reversalJournalEntryId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reversal reason is required.", nameof(reason));

        Status = TransactionStatus.Reversed;
        ReversedJournalEntryId = reversalJournalEntryId;
        ReversalReason = reason.Trim();
        SetModifiedBy(reversedByUserId);
    }

    public void Void(Guid voidedByUserId, string reason)
    {
        if (Status == TransactionStatus.Posted || Status == TransactionStatus.Reversed)
            throw new InvalidOperationException("Cannot void a posted or reversed journal entry. Use reversal instead.");

        Status = TransactionStatus.Voided;
        ReversalReason = reason;
        SetModifiedBy(voidedByUserId);
    }

    public void Submit(Guid submittedByUserId)
    {
        if (Status != TransactionStatus.Draft)
            throw new InvalidOperationException("Only draft entries can be submitted.");

        Status = TransactionStatus.Submitted;
        SetModifiedBy(submittedByUserId);
    }

    public void Approve(Guid approvedByUserId)
    {
        if (Status != TransactionStatus.Submitted)
            throw new InvalidOperationException("Only submitted entries can be approved.");

        Status = TransactionStatus.Approved;
        SetModifiedBy(approvedByUserId);
    }

    private void RecalculateTotals()
    {
        TotalDebit = Lines.Sum(l => l.DebitAmount);
        TotalCredit = Lines.Sum(l => l.CreditAmount);
    }

    public bool IsBalanced => TotalDebit == TotalCredit;
}
