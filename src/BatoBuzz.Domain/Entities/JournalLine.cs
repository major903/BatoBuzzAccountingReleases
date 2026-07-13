namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Represents a single line within a journal entry.
/// Each line specifies a ledger account and either a debit or credit amount.
/// </summary>
public class JournalLine : AuditableEntity
{
    public Guid JournalEntryId { get; internal set; }
    public Guid LedgerId { get; internal set; }
    public decimal DebitAmount { get; internal set; } = 0;
    public decimal CreditAmount { get; internal set; } = 0;
    public string? Narration { get; internal set; }
    public string? CostCentre { get; internal set; }

    // For VAT/Tax
    public decimal? TaxRate { get; internal set; }
    public decimal? TaxAmount { get; private set; }
    public string? TaxCode { get; internal set; }

    // Bank reconciliation
    public bool IsCleared { get; private set; } = false;
    public DateTime? ClearedDate { get; private set; }

    // Navigation
    public JournalEntry JournalEntry { get; private set; } = null!;
    public Ledger Ledger { get; private set; } = null!;

    public void SetCleared(bool cleared, DateTime? clearedDate)
    {
        IsCleared = cleared;
        ClearedDate = cleared ? clearedDate ?? DateTime.UtcNow.Date : null;
    }
}
