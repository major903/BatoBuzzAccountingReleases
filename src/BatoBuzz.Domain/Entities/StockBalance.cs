using BatoBuzz.Domain.Common;

namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Tracks the current stock quantity and value for an item in a specific warehouse.
/// </summary>
public class StockBalance : AuditableEntity
{
    public Guid ItemId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public decimal Quantity { get; private set; } = 0;
    public decimal AverageCost { get; private set; } = 0;
    public decimal TotalValue { get; private set; } = 0;

    // Navigation
    public Item Item { get; private set; } = null!;
    public Warehouse Warehouse { get; private set; } = null!;

    private StockBalance() { }

    public static StockBalance Create(Guid companyId, Guid itemId, Guid warehouseId)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company ID is required.", nameof(companyId));
        if (itemId == Guid.Empty) throw new ArgumentException("Item ID is required.", nameof(itemId));
        if (warehouseId == Guid.Empty) throw new ArgumentException("Warehouse ID is required.", nameof(warehouseId));

        return new StockBalance
        {
            CompanyId = companyId,
            ItemId = itemId,
            WarehouseId = warehouseId
        };
    }

    public void AddStock(decimal quantity, decimal unitCost)
    {
        if (quantity <= 0) return;

        var newTotalValue = Money.Round(TotalValue + quantity * unitCost);
        var newQuantity = Quantity + quantity;

        Quantity = newQuantity;
        AverageCost = newQuantity > 0
            ? Math.Round(newTotalValue / newQuantity, 4, MidpointRounding.AwayFromZero)
            : 0;
        TotalValue = newTotalValue;
    }

    public void AddStockAtValue(decimal quantity, decimal totalValue)
    {
        totalValue = Money.Round(totalValue);
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        if (totalValue < 0)
            throw new ArgumentOutOfRangeException(nameof(totalValue), "Stock value cannot be negative.");

        Quantity += quantity;
        TotalValue = Money.Round(TotalValue + totalValue);
        AverageCost = Quantity > 0
            ? Math.Round(TotalValue / Quantity, 4, MidpointRounding.AwayFromZero)
            : 0;
    }

    public void RemoveStockAtValue(decimal quantity, decimal totalValue)
    {
        totalValue = Money.Round(totalValue);
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        if (totalValue < 0)
            throw new ArgumentOutOfRangeException(nameof(totalValue), "Stock value cannot be negative.");
        if (quantity > Quantity)
            throw new InvalidOperationException($"Insufficient stock. Available: {Quantity}, Requested: {quantity}");
        if (totalValue > TotalValue)
            throw new InvalidOperationException($"Insufficient stock value. Available: {TotalValue}, Requested: {totalValue}");

        Quantity -= quantity;
        TotalValue = Money.Round(TotalValue - totalValue);
        AverageCost = Quantity > 0
            ? Math.Round(TotalValue / Quantity, 4, MidpointRounding.AwayFromZero)
            : 0;
        if (Quantity == 0)
            TotalValue = 0;
    }

    public void RemoveStock(decimal quantity, decimal unitCost, bool allowNegativeStock = false)
    {
        if (quantity <= 0) return;

        if (!allowNegativeStock && quantity > Quantity)
            throw new InvalidOperationException($"Insufficient stock. Available: {Quantity}, Requested: {quantity}");

        Quantity -= quantity;
        TotalValue = Money.Round(Quantity * AverageCost);
    }

    public void AdjustStock(decimal newQuantity, decimal newAverageCost)
    {
        Quantity = newQuantity;
        AverageCost = Math.Round(newAverageCost, 4, MidpointRounding.AwayFromZero);
        TotalValue = Money.Round(newQuantity * AverageCost);
    }
}
