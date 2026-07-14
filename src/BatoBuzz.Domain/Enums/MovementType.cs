namespace BatoBuzz.Domain.Enums;

/// <summary>
/// Types of stock movements in the inventory system.
/// </summary>
public enum MovementType
{
    OpeningStock = 1,
    PurchaseIn = 2,
    SaleOut = 3,
    PurchaseReturn = 4,
    SalesReturn = 5,
    StockAdjustment = 6,
    StockTransferIn = 7,
    StockTransferOut = 8,
    Damage = 9,
    WriteOff = 10
}
