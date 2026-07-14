namespace BatoBuzz.Contracts.Requests;

public class CreateJournalRequest
{
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public DateTime EntryDate { get; set; }
    public int VoucherType { get; set; } = 6; // Journal
    public string? ReferenceNumber { get; set; }
    public string? Narration { get; set; }
    public List<JournalLineRequest> Lines { get; set; } = new();
}

public class JournalLineRequest
{
    public Guid LedgerId { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Narration { get; set; }
    public string? CostCentre { get; set; }
    public decimal? TaxRate { get; set; }
    public string? TaxCode { get; set; }
}

public class CreateSalesInvoiceRequest
{
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public string? Reference { get; set; }
    public string? Narration { get; set; }
    public bool IsVatApplicable { get; set; } = true;
    public decimal VatRate { get; set; } = 13;
    public List<SalesInvoiceLineRequest> Lines { get; set; } = new();
}

public class SalesInvoiceLineRequest
{
    public Guid? ItemId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal TaxPercent { get; set; } = 13;
    public Guid? WarehouseId { get; set; }
}

public class CreateReceiptRequest
{
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime ReceiptDate { get; set; }
    public decimal Amount { get; set; }
    public string? Narration { get; set; }
    public int PaymentMethod { get; set; } = 1;
    public string? ChequeNumber { get; set; }
    public DateTime? ChequeDate { get; set; }
    public string? BankName { get; set; }
    public string? Reference { get; set; }
    public bool IsAdvance { get; set; }
    public List<ReceiptAllocationRequest> Allocations { get; set; } = new();
}

public class ReceiptAllocationRequest
{
    public Guid SalesInvoiceId { get; set; }
    public decimal AmountAllocated { get; set; }
}
