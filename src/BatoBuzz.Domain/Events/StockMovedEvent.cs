using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Domain.Events;

/// <summary>
/// Raised when stock movement occurs.
/// </summary>
public class StockMovedEvent : DomainEvent
{
    public Guid StockMovementId { get; }
    public Guid ItemId { get; }
    public Guid WarehouseId { get; }
    public Guid CompanyId { get; }
    public MovementType MovementType { get; }
    public decimal Quantity { get; }
    public decimal UnitCost { get; }

    public StockMovedEvent(Guid stockMovementId, Guid itemId, Guid warehouseId, Guid companyId, MovementType movementType, decimal quantity, decimal unitCost)
    {
        StockMovementId = stockMovementId;
        ItemId = itemId;
        WarehouseId = warehouseId;
        CompanyId = companyId;
        MovementType = movementType;
        Quantity = quantity;
        UnitCost = unitCost;
    }
}
