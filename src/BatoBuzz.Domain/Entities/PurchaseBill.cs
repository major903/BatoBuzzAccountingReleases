using BatoBuzz.Domain.Common;
using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Represents a purchase bill (supplier invoice) received from a supplier.
/// This is an aggregate root that controls its lifecycle.
/// </summary>
public class PurchaseBill : AuditableEntity, IAggregateRoot
{
    public Guid SupplierId { get; private set; }
    public string BillNumber { get; private set; } = null!;
    public string? SupplierInvoiceNumber { get; private set; }
    public DateTime BillDate { get; private set; }
    public string? BillDateBs { get; private set; }
    public DateTime DueDate { get; private set; }
    public string? Reference { get; private set; }
    public string? Narration { get; private set; }
    public decimal SubTotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal AmountPaid { get; private set; } = 0;
    public decimal DebitedAmount { get; private set; } = 0;
    public decimal BalanceDue { get; private set; }
    public BillStatus Status { get; private set; } = BillStatus.Draft;
    public bool IsVatApplicable { get; private set; } = true;
    public decimal VatRate { get; private set; } = 13;
    public Guid? PostedJournalEntryId { get; private set; }
    public JournalEntry? PostedJournalEntry { get; private set; }

    // Navigation
    public Guid? BranchId { get; private set; }
    public Supplier Supplier { get; private set; } = null!;
    public ICollection<PurchaseBillLine> Lines { get; private set; } = new List<PurchaseBillLine>();
    public ICollection<PaymentAllocation> PaymentAllocations { get; private set; } = new List<PaymentAllocation>();

    private PurchaseBill() { }

    public static PurchaseBill Create(
        Guid companyId,
        Guid supplierId,
        string billNumber,
        DateTime billDate,
        DateTime dueDate,
        Guid createdByUserId,
        string? supplierInvoiceNumber = null,
        string? reference = null,
        string? narration = null,
        bool isVatApplicable = true,
        decimal vatRate = 13,
        Guid? branchId = null,
        string? billDateBs = null)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company ID is required.", nameof(companyId));
        if (supplierId == Guid.Empty) throw new ArgumentException("Supplier ID is required.", nameof(supplierId));
        if (string.IsNullOrWhiteSpace(billNumber)) throw new ArgumentException("Bill number is required.", nameof(billNumber));
        if (dueDate.Date < billDate.Date) throw new ArgumentException("Due date cannot be before bill date.", nameof(dueDate));
        if (vatRate < 0 || vatRate > 100) throw new ArgumentOutOfRangeException(nameof(vatRate), "VAT rate must be between 0 and 100.");

        return new PurchaseBill
        {
            CompanyId = companyId,
            SupplierId = supplierId,
            BillNumber = billNumber,
            SupplierInvoiceNumber = supplierInvoiceNumber,
            BillDate = billDate,
            DueDate = dueDate,
            Reference = reference,
            Narration = narration,
            IsVatApplicable = isVatApplicable,
            VatRate = vatRate,
            BranchId = branchId,
            BillDateBs = billDateBs,
            CreatedByUserId = createdByUserId
        };
    }

    public PurchaseBillLine AddLine(Guid? itemId, string description, decimal quantity, decimal rate, decimal discountPercent, decimal taxPercent, Guid? warehouseId = null)
    {
        if (Status != BillStatus.Draft)
            throw new InvalidOperationException("Can only add lines to draft bills.");

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

        var line = new PurchaseBillLine
        {
            CompanyId = CompanyId,
            PurchaseBillId = Id,
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
        Guid supplierId,
        DateTime billDate,
        DateTime dueDate,
        string? supplierInvoiceNumber,
        string? reference,
        string? narration,
        bool isVatApplicable,
        decimal vatRate,
        Guid? branchId,
        Guid modifiedByUserId)
    {
        if (Status != BillStatus.Draft)
            throw new InvalidOperationException("Only draft bills can be edited.");
        if (supplierId == Guid.Empty)
            throw new ArgumentException("Supplier ID is required.", nameof(supplierId));
        if (dueDate.Date < billDate.Date)
            throw new ArgumentException("Due date cannot be before bill date.", nameof(dueDate));
        if (vatRate < 0 || vatRate > 100)
            throw new ArgumentOutOfRangeException(nameof(vatRate), "VAT rate must be between 0 and 100.");

        SupplierId = supplierId;
        BillDate = billDate;
        DueDate = dueDate;
        SupplierInvoiceNumber = supplierInvoiceNumber;
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

    public void Receive()
    {
        if (Status != BillStatus.Draft)
            throw new InvalidOperationException("Only draft bills can be received.");
        if (Lines.Count == 0 || TotalAmount <= 0)
            throw new InvalidOperationException("A purchase bill must have at least one positive-value line before it can be received.");
        Status = BillStatus.Received;
    }

    public void AttachPostedJournal(Guid journalEntryId)
    {
        if (journalEntryId == Guid.Empty)
            throw new ArgumentException("Posted journal entry ID is required.", nameof(journalEntryId));
        if (PostedJournalEntryId.HasValue && PostedJournalEntryId != journalEntryId)
            throw new InvalidOperationException("The bill is already linked to another posted journal.");

        PostedJournalEntryId = journalEntryId;
    }

    public void RecordPayment(decimal amount)
    {
        amount = Money.Round(amount);
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment allocation must be greater than zero.");
        if (Status is not (BillStatus.Received or BillStatus.PartiallyPaid or BillStatus.Overdue))
            throw new InvalidOperationException("Payments can only be allocated to a received, partially paid, or overdue bill.");
        if (amount > BalanceDue)
            throw new InvalidOperationException("Payment allocation exceeds the bill balance due.");

        AmountPaid = Money.Round(AmountPaid + amount);
        BalanceDue = Money.Round(TotalAmount - AmountPaid - DebitedAmount);

        if (BalanceDue <= 0)
            Status = BillStatus.Paid;
        else if (AmountPaid > 0)
            Status = BillStatus.PartiallyPaid;

    }

    public void UnapplyPayment(decimal amount, DateTime statusAsOfDate)
    {
        amount = Money.Round(amount);
        if (amount <= 0 || amount > AmountPaid)
            throw new InvalidOperationException("Payment reversal exceeds the amount applied to the bill.");

        AmountPaid = Money.Round(AmountPaid - amount);
        BalanceDue = Money.Round(TotalAmount - AmountPaid - DebitedAmount);
        if (BalanceDue <= 0)
            Status = BillStatus.Paid;
        else if (AmountPaid > 0)
            Status = BillStatus.PartiallyPaid;
        else
            Status = statusAsOfDate.Date > DueDate.Date ? BillStatus.Overdue : BillStatus.Received;
    }

    public void Cancel(Guid cancelledByUserId)
    {
        if (AmountPaid != 0)
            throw new InvalidOperationException("Cannot cancel a bill with payments. Reverse payments first.");
        if (Status is not (BillStatus.Received or BillStatus.Overdue))
            throw new InvalidOperationException("Only a received or overdue bill can be cancelled.");

        Status = BillStatus.Cancelled;
        SetModifiedBy(cancelledByUserId);
    }

    public void IssueDebitNote(decimal amount, Guid userId)
    {
        if (AmountPaid != 0)
            throw new InvalidOperationException("Cannot issue a debit note against a bill with payments. Reverse payments first.");
        if (Status is not (BillStatus.Received or BillStatus.Overdue))
            throw new InvalidOperationException("A debit note requires a received or overdue bill that has not already been corrected.");

        amount = Money.Round(amount);
        if (amount <= 0 || amount > BalanceDue)
            throw new InvalidOperationException("Debit note amount must be positive and cannot exceed the purchase bill balance due.");
        DebitedAmount = Money.Round(DebitedAmount + amount);
        BalanceDue = Money.Round(TotalAmount - AmountPaid - DebitedAmount);
        if (BalanceDue == 0)
            Status = BillStatus.DebitNoteIssued;
        SetModifiedBy(userId);
    }

    private void RecalculateTotals()
    {
        SubTotal = Money.Round(Lines.Sum(l => Money.Round(l.Quantity * l.Rate)));
        DiscountAmount = Money.Round(Lines.Sum(l => l.DiscountAmount));
        TaxableAmount = Money.Round(Lines.Sum(l => l.NetAmount));
        VatAmount = Money.Round(Lines.Sum(l => l.TaxAmount));
        TotalAmount = Money.Round(Lines.Sum(l => l.LineTotal));
        BalanceDue = Money.Round(TotalAmount - AmountPaid - DebitedAmount);
    }
}
