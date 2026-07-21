using BatoBuzz.Domain.Common;
using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Represents a payment made to a supplier.
/// Can be allocated to specific bills or recorded as an advance.
/// </summary>
public class Payment : AuditableEntity, IAggregateRoot
{
    public Guid SupplierId { get; private set; }
    public string PaymentNumber { get; private set; } = null!;
    public DateTime PaymentDate { get; private set; }
    public decimal Amount { get; private set; }
    /// <summary>TDS withheld on top of the cash <see cref="Amount"/>. Together they clear the supplier's payable.</summary>
    public decimal TdsAmount { get; private set; }
    public string? Narration { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; } = PaymentMethod.Cash;
    public string? ChequeNumber { get; private set; }
    public DateTime? ChequeDate { get; private set; }
    public string? BankName { get; private set; }
    public string? Reference { get; private set; }
    public bool IsAdvance { get; private set; } = false;
    public Guid? PostedJournalEntryId { get; private set; }
    public JournalEntry? PostedJournalEntry { get; private set; }

    // Navigation
    public Supplier Supplier { get; private set; } = null!;
    public ICollection<PaymentAllocation> Allocations { get; private set; } = new List<PaymentAllocation>();

    private Payment() { }

    public static Payment Create(
        Guid companyId,
        Guid supplierId,
        string paymentNumber,
        DateTime paymentDate,
        decimal amount,
        Guid createdByUserId,
        string? narration = null,
        PaymentMethod paymentMethod = PaymentMethod.Cash,
        string? chequeNumber = null,
        DateTime? chequeDate = null,
        string? bankName = null,
        string? reference = null,
        bool isAdvance = false,
        decimal tdsAmount = 0)
    {
        amount = Money.Round(amount);
        tdsAmount = Money.Round(tdsAmount);
        if (companyId == Guid.Empty) throw new ArgumentException("Company ID is required.", nameof(companyId));
        if (supplierId == Guid.Empty) throw new ArgumentException("Supplier ID is required.", nameof(supplierId));
        if (string.IsNullOrWhiteSpace(paymentNumber)) throw new ArgumentException("Payment number is required.", nameof(paymentNumber));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be greater than zero.");
        if (tdsAmount < 0) throw new ArgumentOutOfRangeException(nameof(tdsAmount), "TDS amount cannot be negative.");
        if (!Enum.IsDefined(typeof(PaymentMethod), paymentMethod)) throw new ArgumentOutOfRangeException(nameof(paymentMethod), "Payment method is invalid.");

        return new Payment
        {
            CompanyId = companyId,
            SupplierId = supplierId,
            PaymentNumber = paymentNumber,
            PaymentDate = paymentDate,
            Amount = amount,
            TdsAmount = tdsAmount,
            Narration = narration,
            PaymentMethod = paymentMethod,
            ChequeNumber = chequeNumber,
            ChequeDate = chequeDate,
            BankName = bankName,
            Reference = reference,
            IsAdvance = isAdvance,
            CreatedByUserId = createdByUserId
        };
    }

    public void AllocateToBill(Guid purchaseBillId, decimal amount)
    {
        amount = Money.Round(amount);
        if (purchaseBillId == Guid.Empty)
            throw new ArgumentException("Purchase bill ID is required.", nameof(purchaseBillId));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Allocation amount must be greater than zero.");
        if (Allocations.Any(a => a.PurchaseBillId == purchaseBillId))
            throw new InvalidOperationException("A payment cannot contain duplicate allocations for the same bill.");

        var totalAllocated = Allocations.Sum(a => a.AmountAllocated);
        if (totalAllocated + amount > Amount + TdsAmount)
            throw new InvalidOperationException("Allocation exceeds payment amount.");

        Allocations.Add(new PaymentAllocation
        {
            CompanyId = CompanyId,
            PaymentId = Id,
            PurchaseBillId = purchaseBillId,
            AmountAllocated = amount
        });

        IsAdvance = false;
    }

    public void AttachPostedJournal(Guid journalEntryId)
    {
        if (journalEntryId == Guid.Empty)
            throw new ArgumentException("Posted journal entry ID is required.", nameof(journalEntryId));
        if (PostedJournalEntryId.HasValue && PostedJournalEntryId != journalEntryId)
            throw new InvalidOperationException("The payment is already linked to another posted journal.");

        PostedJournalEntryId = journalEntryId;
    }
}
