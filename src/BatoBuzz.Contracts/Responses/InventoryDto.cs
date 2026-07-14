using BatoBuzz.Contracts.Common;

namespace BatoBuzz.Contracts.Responses;

public class ItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Code { get; set; }
    public string? Barcode { get; set; }
    public ItemTypeDto ItemType { get; set; }
    public string? CategoryName { get; set; }
    public string UnitName { get; set; } = null!;
    public decimal? ReorderLevel { get; set; }
    public decimal SalePrice { get; set; }
    public decimal StandardCost { get; set; }
    public bool IsActive { get; set; }
    public bool IsLowStock { get; set; }
}

public class StockBalanceDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal TotalValue { get; set; }
}

public class WarehouseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Code { get; set; }
    public bool IsDefault { get; set; }
}

public class UnitDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? ShortName { get; set; }
}

public class ItemCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid? ParentCategoryId { get; set; }
}
