using BatoBuzz.Contracts.Common;

namespace BatoBuzz.Contracts.Responses;

public class SalesInvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public DateTime InvoiceDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountReceived { get; set; }
    public decimal BalanceDue { get; set; }
    public InvoiceStatusDto Status { get; set; }
    public Guid? PostedJournalEntryId { get; set; }
    public DateTime? CorrectionDate { get; set; }
    public string? CorrectionReason { get; set; }
    public List<SalesInvoiceLineDto> Lines { get; set; } = new();
}

public class SalesInvoiceLineDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class ReceiptDto
{
    public Guid Id { get; set; }
    public string ReceiptNumber { get; set; } = null!;
    public DateTime ReceiptDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public decimal Amount { get; set; }
    public PaymentMethodDto PaymentMethod { get; set; }
    public Guid? PostedJournalEntryId { get; set; }
    public TransactionStatusDto Status { get; set; }
    public DateTime? CorrectionDate { get; set; }
    public string? CorrectionReason { get; set; }
}

public class PurchaseBillDto
{
    public Guid Id { get; set; }
    public string BillNumber { get; set; } = null!;
    public DateTime BillDate { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public decimal SubTotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public BillStatusDto Status { get; set; }
    public Guid? PostedJournalEntryId { get; set; }
    public DateTime? CorrectionDate { get; set; }
    public string? CorrectionReason { get; set; }
}

public class PaymentDto
{
    public Guid Id { get; set; }
    public string PaymentNumber { get; set; } = null!;
    public DateTime PaymentDate { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal TdsAmount { get; set; }
    public PaymentMethodDto PaymentMethod { get; set; }
    public Guid? PostedJournalEntryId { get; set; }
    public TransactionStatusDto Status { get; set; }
    public DateTime? CorrectionDate { get; set; }
    public string? CorrectionReason { get; set; }
}

public class TdsRateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal RatePercent { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
