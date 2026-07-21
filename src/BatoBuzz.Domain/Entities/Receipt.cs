using BatoBuzz.Domain.Common;
using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Represents a payment received from a customer.
/// Can be allocated to specific invoices or recorded as an advance.
/// </summary>
public class Receipt : AuditableEntity, IAggregateRoot
{
    public Guid CustomerId { get; private set; }
    public string ReceiptNumber { get; private set; } = null!;
    public DateTime ReceiptDate { get; private set; }
    public decimal Amount { get; private set; }
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
    public Customer Customer { get; private set; } = null!;
    public ICollection<ReceiptAllocation> Allocations { get; private set; } = new List<ReceiptAllocation>();

    private Receipt() { }

    public static Receipt Create(
        Guid companyId,
        Guid customerId,
        string receiptNumber,
        DateTime receiptDate,
        decimal amount,
        Guid createdByUserId,
        string? narration = null,
        PaymentMethod paymentMethod = PaymentMethod.Cash,
        string? chequeNumber = null,
        DateTime? chequeDate = null,
        string? bankName = null,
        string? reference = null,
        bool isAdvance = false)
    {
        amount = Money.Round(amount);
        if (companyId == Guid.Empty) throw new ArgumentException("Company ID is required.", nameof(companyId));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer ID is required.", nameof(customerId));
        if (string.IsNullOrWhiteSpace(receiptNumber)) throw new ArgumentException("Receipt number is required.", nameof(receiptNumber));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Receipt amount must be greater than zero.");
        if (!Enum.IsDefined(typeof(PaymentMethod), paymentMethod)) throw new ArgumentOutOfRangeException(nameof(paymentMethod), "Payment method is invalid.");

        return new Receipt
        {
            CompanyId = companyId,
            CustomerId = customerId,
            ReceiptNumber = receiptNumber,
            ReceiptDate = receiptDate,
            Amount = amount,
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

    public void AllocateToInvoice(Guid salesInvoiceId, decimal amount)
    {
        amount = Money.Round(amount);
        if (salesInvoiceId == Guid.Empty)
            throw new ArgumentException("Sales invoice ID is required.", nameof(salesInvoiceId));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Allocation amount must be greater than zero.");
        if (Allocations.Any(a => a.SalesInvoiceId == salesInvoiceId))
            throw new InvalidOperationException("A receipt cannot contain duplicate allocations for the same invoice.");

        var totalAllocated = Allocations.Sum(a => a.AmountAllocated);
        if (totalAllocated + amount > Amount)
            throw new InvalidOperationException("Allocation exceeds receipt amount.");

        Allocations.Add(new ReceiptAllocation
        {
            CompanyId = CompanyId,
            ReceiptId = Id,
            SalesInvoiceId = salesInvoiceId,
            AmountAllocated = amount
        });

        IsAdvance = false;
    }

    public void AttachPostedJournal(Guid journalEntryId)
    {
        if (journalEntryId == Guid.Empty)
            throw new ArgumentException("Posted journal entry ID is required.", nameof(journalEntryId));
        if (PostedJournalEntryId.HasValue && PostedJournalEntryId != journalEntryId)
            throw new InvalidOperationException("The receipt is already linked to another posted journal.");

        PostedJournalEntryId = journalEntryId;
    }
}
