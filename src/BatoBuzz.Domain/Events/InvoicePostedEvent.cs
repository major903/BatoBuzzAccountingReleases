namespace BatoBuzz.Domain.Events;

/// <summary>
/// Raised when a sales invoice is posted to the general ledger.
/// </summary>
public class InvoicePostedEvent : DomainEvent
{
    public Guid SalesInvoiceId { get; }
    public Guid CompanyId { get; }
    public Guid CustomerId { get; }
    public string InvoiceNumber { get; }
    public decimal TotalAmount { get; }

    public InvoicePostedEvent(Guid salesInvoiceId, Guid companyId, Guid customerId, string invoiceNumber, decimal totalAmount)
    {
        SalesInvoiceId = salesInvoiceId;
        CompanyId = companyId;
        CustomerId = customerId;
        InvoiceNumber = invoiceNumber;
        TotalAmount = totalAmount;
    }
}
