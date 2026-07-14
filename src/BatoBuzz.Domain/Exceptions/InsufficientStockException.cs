namespace BatoBuzz.Domain.Exceptions;

/// <summary>
/// Thrown when an operation requires more stock than is available.
/// </summary>
public class InsufficientStockException : DomainException
{
    public Guid ItemId { get; }
    public Guid WarehouseId { get; }
    public decimal RequestedQuantity { get; }
    public decimal AvailableQuantity { get; }

    public InsufficientStockException(Guid itemId, Guid warehouseId, decimal requested, decimal available)
        : base($"Insufficient stock for item {itemId} in warehouse {warehouseId}. Requested: {requested}, Available: {available}")
    {
        ItemId = itemId;
        WarehouseId = warehouseId;
        RequestedQuantity = requested;
        AvailableQuantity = available;
    }
}
