using BatoBuzz.Domain.Common;
using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Represents a sales invoice (bill) issued to a customer.
/// This is an aggregate root that controls its lifecycle and postings.
/// </summary>
public class SalesInvoice : AuditableEntity, IAggregateRoot
{
    public Guid CustomerId { get; private set; }
    public string InvoiceNumber { get; private set; } = null!;
    public DateTime InvoiceDate { get; private set; }
    public string? InvoiceDateBs { get; private set; }
    public DateTime DueDate { get; private set; }
    public string? Reference { get; private set; }
    public string? Narration { get; private set; }
    public decimal SubTotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal AmountReceived { get; private set; } = 0;
    public decimal CreditedAmount { get; private set; } = 0;
    public decimal BalanceDue { get; private set; }
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;
    public bool IsVatApplicable { get; private set; } = true;
    public decimal VatRate { get; private set; } = 13; // Nepal VAT default 13%
    public Guid? PostedJournalEntryId { get; private set; }
    public JournalEntry? PostedJournalEntry { get; private set; }

    // Navigation
    public Guid? BranchId { get; private set; }
    public Customer Customer { get; private set; } = null!;
    public ICollection<SalesInvoiceLine> Lines { get; private set; } = new List<SalesInvoiceLine>();
    public ICollection<ReceiptAllocation> ReceiptAllocations { get; private set; } = new List<ReceiptAllocation>();

    private SalesInvoice() { }

    public static SalesInvoice Create(
        Guid companyId,
        Guid customerId,
        string invoiceNumber,
        DateTime invoiceDate,
        DateTime dueDate,
        Guid createdByUserId,
        string? reference = null,
        string? narration = null,
        bool isVatApplicable = true,
        decimal vatRate = 13,
        Guid? branchId = null,
        string? invoiceDateBs = null)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company ID is required.", nameof(companyId));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer ID is required.", nameof(customerId));
        if (string.IsNullOrWhiteSpace(invoiceNumber)) throw new ArgumentException("Invoice number is required.", nameof(invoiceNumber));
        if (dueDate.Date < invoiceDate.Date) throw new ArgumentException("Due date cannot be before invoice date.", nameof(dueDate));
        if (vatRate < 0 || vatRate > 100) throw new ArgumentOutOfRangeException(nameof(vatRate), "VAT rate must be between 0 and 100.");

        return new SalesInvoice
        {
            CompanyId = companyId,
            CustomerId = customerId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            Reference = reference,
            Narration = narration,
            IsVatApplicable = isVatApplicable,
            VatRate = vatRate,
            BranchId = branchId,
            InvoiceDateBs = invoiceDateBs,
            CreatedByUserId = createdByUserId
        };
    }

    public SalesInvoiceLine AddLine(Guid? itemId, string description, decimal quantity, decimal rate, decimal discountPercent, decimal taxPercent, Guid? warehouseId = null)
    {
        if (Status != InvoiceStatus.Draft)
            throw new InvalidOperationException("Can only add lines to draft invoices.");

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Line description is required.", nameof(description));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        if (rate < 0)
            throw new ArgumentOutOfRangeException(nameof(rate), "Rate cannot be negative.");
        if (discountPercent < 0 || discountPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(discountPercent), "Discount percent must be between 0 and 100.");
        if (taxPercent < 0 || taxPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(taxPercent), "Tax percent must be between 0 and 100.");
        if (!itemId.HasValue && warehouseId.HasValue)
            throw new ArgumentException("A warehouse can only be specified for an item line.", nameof(warehouseId));

        rate = Money.Round(rate);
        var grossAmount = Money.Round(quantity * rate);
        var discountAmount = Money.Round(grossAmount * discountPercent / 100);
        var netAmount = Money.Round(grossAmount - discountAmount);
        var taxAmount = IsVatApplicable ? Money.Round(netAmount * taxPercent / 100) : 0;
        var lineTotal = Money.Round(netAmount + taxAmount);

        var line = new SalesInvoiceLine
        {
            CompanyId = CompanyId,
            SalesInvoiceId = Id,
            ItemId = itemId,
            Description = description,
            Quantity = quantity,
            Rate = rate,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            NetAmount = netAmount,
            TaxPercent = taxPercent,
            TaxAmount = taxAmount,
            LineTotal = lineTotal,
            WarehouseId = warehouseId
        };

        Lines.Add(line);
        RecalculateTotals();
        return line;
    }

    public void UpdateDraft(
        Guid customerId,
        DateTime invoiceDate,
        DateTime dueDate,
        string? reference,
        string? narration,
        bool isVatApplicable,
        decimal vatRate,
        Guid? branchId,
        Guid modifiedByUserId)
    {
        if (Status != InvoiceStatus.Draft)
            throw new InvalidOperationException("Only draft invoices can be edited.");
        if (customerId == Guid.Empty)
            throw new ArgumentException("Customer ID is required.", nameof(customerId));
        if (dueDate.Date < invoiceDate.Date)
            throw new ArgumentException("Due date cannot be before invoice date.", nameof(dueDate));
        if (vatRate < 0 || vatRate > 100)
            throw new ArgumentOutOfRangeException(nameof(vatRate), "VAT rate must be between 0 and 100.");

        CustomerId = customerId;
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        Reference = reference;
        Narration = narration;
        IsVatApplicable = isVatApplicable;
        VatRate = vatRate;
        BranchId = branchId;
        Lines.Clear();
        SubTotal = 0;
        DiscountAmount = 0;
        TaxableAmount = 0;
        VatAmount = 0;
        TotalAmount = 0;
        BalanceDue = 0;
        SetModifiedBy(modifiedByUserId);
    }

    public void Issue()
    {
        if (Status != InvoiceStatus.Draft)
            throw new InvalidOperationException("Only draft invoices can be issued.");
        if (Lines.Count == 0 || TotalAmount <= 0)
            throw new InvalidOperationException("An invoice must have at least one positive-value line before it can be issued.");

        Status = InvoiceStatus.Issued;
    }

    public void AttachPostedJournal(Guid journalEntryId)
    {
        if (journalEntryId == Guid.Empty)
            throw new ArgumentException("Posted journal entry ID is required.", nameof(journalEntryId));
        if (PostedJournalEntryId.HasValue && PostedJournalEntryId != journalEntryId)
            throw new InvalidOperationException("The invoice is already linked to another posted journal.");

        PostedJournalEntryId = journalEntryId;
    }

    public void RecordReceipt(decimal amount)
    {
        amount = Money.Round(amount);
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Receipt allocation must be greater than zero.");
        if (Status is not (InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid or InvoiceStatus.Overdue))
            throw new InvalidOperationException("Receipts can only be allocated to an issued, partially paid, or overdue invoice.");
        if (amount > BalanceDue)
            throw new InvalidOperationException("Receipt allocation exceeds the invoice balance due.");

        AmountReceived = Money.Round(AmountReceived + amount);
        BalanceDue = Money.Round(TotalAmount - AmountReceived - CreditedAmount);

        if (BalanceDue <= 0)
            Status = InvoiceStatus.Paid;
        else if (AmountReceived > 0)
            Status = InvoiceStatus.PartiallyPaid;

    }

    public void UnapplyReceipt(decimal amount, DateTime statusAsOfDate)
    {
        amount = Money.Round(amount);
        if (amount <= 0 || amount > AmountReceived)
            throw new InvalidOperationException("Receipt reversal exceeds the amount applied to the invoice.");

        AmountReceived = Money.Round(AmountReceived - amount);
        BalanceDue = Money.Round(TotalAmount - AmountReceived - CreditedAmount);
        if (BalanceDue <= 0)
            Status = InvoiceStatus.Paid;
        else if (AmountReceived > 0)
            Status = InvoiceStatus.PartiallyPaid;
        else
            Status = statusAsOfDate.Date > DueDate.Date ? InvoiceStatus.Overdue : InvoiceStatus.Issued;
    }

    public void Cancel(Guid cancelledByUserId)
    {
        if (AmountReceived != 0)
            throw new InvalidOperationException("Cannot cancel an invoice with receipts. Reverse receipts first.");
        if (Status is not (InvoiceStatus.Issued or InvoiceStatus.Overdue))
            throw new InvalidOperationException("Only an issued or overdue invoice can be cancelled.");

        Status = InvoiceStatus.Cancelled;
        SetModifiedBy(cancelledByUserId);
    }

    public void IssueCreditNote(decimal amount, Guid userId)
    {
        if (AmountReceived != 0)
            throw new InvalidOperationException("Cannot issue a credit note against an invoice with receipts. Reverse receipts first.");
        if (Status is not (InvoiceStatus.Issued or InvoiceStatus.Overdue))
            throw new InvalidOperationException("A credit note requires an issued or overdue invoice that has not already been corrected.");

        amount = Money.Round(amount);
        if (amount <= 0 || amount > BalanceDue)
            throw new InvalidOperationException("Credit note amount must be positive and cannot exceed the invoice balance due.");
        CreditedAmount = Money.Round(CreditedAmount + amount);
        BalanceDue = Money.Round(TotalAmount - AmountReceived - CreditedAmount);
        if (BalanceDue == 0)
            Status = InvoiceStatus.CreditNoteIssued;
        SetModifiedBy(userId);
    }

    public void CheckOverdue(DateTime asOfDate)
    {
        if (Status == InvoiceStatus.Issued || Status == InvoiceStatus.PartiallyPaid)
        {
            if (asOfDate > DueDate)
                Status = InvoiceStatus.Overdue;
        }
    }

    private void RecalculateTotals()
    {
        SubTotal = Money.Round(Lines.Sum(l => Money.Round(l.Quantity * l.Rate)));
        DiscountAmount = Money.Round(Lines.Sum(l => l.DiscountAmount));
        TaxableAmount = Money.Round(Lines.Sum(l => l.NetAmount));
        VatAmount = Money.Round(Lines.Sum(l => l.TaxAmount));
        TotalAmount = Money.Round(Lines.Sum(l => l.LineTotal));
        BalanceDue = Money.Round(TotalAmount - AmountReceived - CreditedAmount);
    }
}
