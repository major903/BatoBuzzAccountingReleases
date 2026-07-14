namespace BatoBuzz.Contracts.Requests;

public class CreatePurchaseBillRequest
{
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid SupplierId { get; set; }
    public DateTime BillDate { get; set; }
    public DateTime DueDate { get; set; }
    public string? SupplierInvoiceNumber { get; set; }
    public string? Reference { get; set; }
    public string? Narration { get; set; }
    public bool IsVatApplicable { get; set; } = true;
    public decimal VatRate { get; set; } = 13;
    public List<PurchaseBillLineRequest> Lines { get; set; } = new();
}

public class PurchaseBillLineRequest
{
    public Guid? ItemId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal TaxPercent { get; set; } = 13;
    public Guid? WarehouseId { get; set; }
}

public class CreatePaymentRequest
{
    public Guid CompanyId { get; set; }
    public Guid SupplierId { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    /// <summary>TDS withheld on top of <see cref="Amount"/>. Together they clear the supplier's payable.</summary>
    public decimal TdsAmount { get; set; }
    public string? Narration { get; set; }
    public int PaymentMethod { get; set; } = 1;
    public string? ChequeNumber { get; set; }
    public DateTime? ChequeDate { get; set; }
    public string? BankName { get; set; }
    public string? Reference { get; set; }
    public bool IsAdvance { get; set; }
    public List<PaymentAllocationRequest> Allocations { get; set; } = new();
}

public class PaymentAllocationRequest
{
    public Guid PurchaseBillId { get; set; }
    public decimal AmountAllocated { get; set; }
}

public class CreateTdsRateRequest
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public decimal RatePercent { get; set; }
    public string? Description { get; set; }
}
