namespace BatoBuzz.Contracts.Requests;

public class CreateItemRequest
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string? NameNepali { get; set; }
    public string? Code { get; set; }
    public string? Barcode { get; set; }
    public string? Description { get; set; }
    public int ItemType { get; set; } = 1;
    public Guid? CategoryId { get; set; }
    public Guid UnitId { get; set; }
    public decimal? ReorderLevel { get; set; }
    public decimal? ReorderQuantity { get; set; }
    public decimal StandardCost { get; set; }
    public decimal SalePrice { get; set; }
    public bool AllowNegativeStock { get; set; }
    public int CostingMethod { get; set; } = 2;
}

public class CreateStockMovementRequest
{
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime MovementDate { get; set; }
    public int MovementType { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? Narration { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public class CreateStockAdjustmentRequest
{
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime AdjustmentDate { get; set; }
    public decimal AdjustedQuantity { get; set; }
    public string? Reason { get; set; }
    public string? Narration { get; set; }
}

public class CreateItemCategoryRequest
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public Guid? ParentCategoryId { get; set; }
}

public class CreateUnitRequest
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string? ShortName { get; set; }
}

public class CreateWarehouseRequest
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string? Code { get; set; }
    public string? Address { get; set; }
    public bool IsDefault { get; set; }
}
