using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Represents a product, service, or asset in the inventory system.
/// </summary>
public class Item : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string? NameNepali { get; private set; }
    public string? Code { get; private set; }
    public string? Barcode { get; private set; }
    public string? Description { get; private set; }
    public ItemType ItemType { get; private set; } = ItemType.Goods;
    public Guid? CategoryId { get; private set; }
    public Guid UnitId { get; private set; }
    public decimal? ReorderLevel { get; private set; }
    public decimal? ReorderQuantity { get; private set; }
    public decimal StandardCost { get; private set; } = 0;
    public decimal SalePrice { get; private set; } = 0;
    public bool IsActive { get; private set; } = true;
    public bool AllowNegativeStock { get; private set; } = false;
    public CostingMethod CostingMethod { get; private set; } = CostingMethod.WeightedAverage;

    // Navigation
    public ItemCategory? Category { get; private set; }
    public Unit Unit { get; private set; } = null!;
    public ICollection<StockBalance> StockBalances { get; private set; } = new List<StockBalance>();
    public ICollection<StockMovement> StockMovements { get; private set; } = new List<StockMovement>();

    private Item() { }

    public static Item Create(
        Guid companyId,
        string name,
        Guid unitId,
        ItemType itemType = ItemType.Goods,
        string? code = null,
        string? nameNepali = null,
        string? barcode = null,
        string? description = null,
        Guid? categoryId = null,
        decimal? reorderLevel = null,
        decimal? reorderQuantity = null,
        decimal standardCost = 0,
        decimal salePrice = 0,
        bool allowNegativeStock = false,
        CostingMethod costingMethod = CostingMethod.WeightedAverage)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("Company ID is required.", nameof(companyId));
        if (unitId == Guid.Empty)
            throw new ArgumentException("Unit ID is required.", nameof(unitId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Item name is required.", nameof(name));
        if (!Enum.IsDefined(typeof(ItemType), itemType))
            throw new ArgumentOutOfRangeException(nameof(itemType), "Item type is invalid.");
        if (costingMethod != CostingMethod.WeightedAverage)
            throw new ArgumentOutOfRangeException(nameof(costingMethod), "Only weighted-average costing is supported.");
        if (allowNegativeStock)
            throw new ArgumentException("Negative inventory is not supported with weighted-average costing.", nameof(allowNegativeStock));
        if (standardCost < 0 || salePrice < 0 || reorderLevel is < 0 || reorderQuantity is < 0)
            throw new ArgumentOutOfRangeException(nameof(standardCost), "Item prices and reorder values cannot be negative.");

        return new Item
        {
            CompanyId = companyId,
            Name = name,
            UnitId = unitId,
            ItemType = itemType,
            Code = code,
            NameNepali = nameNepali,
            Barcode = barcode,
            Description = description,
            CategoryId = categoryId,
            ReorderLevel = reorderLevel,
            ReorderQuantity = reorderQuantity,
            StandardCost = standardCost,
            SalePrice = salePrice,
            AllowNegativeStock = allowNegativeStock,
            CostingMethod = costingMethod
        };
    }

    public void Update(string? name = null, decimal? salePrice = null, decimal? standardCost = null, bool? isActive = null)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name;
        if (salePrice.HasValue) SalePrice = salePrice.Value;
        if (standardCost.HasValue) StandardCost = standardCost.Value;
        if (isActive.HasValue) IsActive = isActive.Value;
    }

    public void UpdateDetails(string name, string? code, decimal standardCost, decimal salePrice, decimal? reorderLevel, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Item name is required.", nameof(name));
        if (standardCost < 0 || salePrice < 0 || reorderLevel is < 0)
            throw new ArgumentOutOfRangeException(nameof(standardCost), "Item prices and reorder level cannot be negative.");

        Name = name.Trim();
        Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        StandardCost = standardCost;
        SalePrice = salePrice;
        ReorderLevel = reorderLevel;
        IsActive = isActive;
    }

    public bool IsLowStock => ItemType == ItemType.Goods && ReorderLevel.HasValue &&
        StockBalances.Sum(sb => sb.Quantity) <= ReorderLevel.Value;
}
