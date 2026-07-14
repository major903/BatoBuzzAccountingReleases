namespace BatoBuzz.Domain.Exceptions;

/// <summary>
/// Thrown when a payment allocation is invalid.
/// </summary>
public class InvalidAllocationException : DomainException
{
    public Guid PaymentId { get; }
    public Guid DocumentId { get; }
    public decimal AllocationAmount { get; }

    public InvalidAllocationException(Guid paymentId, Guid documentId, decimal allocationAmount, string reason)
        : base($"Invalid allocation of {allocationAmount:N2} from payment {paymentId} to document {documentId}: {reason}")
    {
        PaymentId = paymentId;
        DocumentId = documentId;
        AllocationAmount = allocationAmount;
    }
}
