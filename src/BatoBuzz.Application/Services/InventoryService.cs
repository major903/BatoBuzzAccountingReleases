using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Common;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Contracts.Responses;
using BatoBuzz.Domain.Entities;
using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AccountingPostingHelper _posting;

    public InventoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _posting = new AccountingPostingHelper(unitOfWork);
    }

    public async Task<ItemDto> CreateItemAsync(CreateItemRequest request, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Item name is required.");
        if (!Enum.IsDefined(typeof(ItemType), request.ItemType))
            throw new InvalidOperationException("Item type is invalid.");
        if (request.CostingMethod != (int)CostingMethod.WeightedAverage)
            throw new InvalidOperationException("Only weighted-average inventory costing is currently supported.");
        if (request.AllowNegativeStock)
            throw new InvalidOperationException("Negative inventory is not supported with weighted-average costing.");
        if (request.StandardCost < 0 || request.SalePrice < 0)
            throw new InvalidOperationException("Item cost and sale price cannot be negative.");
        if (request.ReorderLevel is < 0 || request.ReorderQuantity is < 0)
            throw new InvalidOperationException("Reorder values cannot be negative.");

        var company = await _unitOfWork.Companies.GetByIdWithDetailsAsync(request.CompanyId)
            ?? throw new InvalidOperationException("Company not found.");
        if (!company.IsActive)
            throw new InvalidOperationException("Company is inactive.");
        if (company.Units.All(u => u.Id != request.UnitId || !u.IsActive))
            throw new InvalidOperationException("Unit does not belong to the selected company or is inactive.");
        if (request.CategoryId.HasValue && company.ItemCategories.All(c => c.Id != request.CategoryId.Value || !c.IsActive))
            throw new InvalidOperationException("Item category does not belong to the selected company or is inactive.");

        var item = Item.Create(
            request.CompanyId, request.Name.Trim(), request.UnitId,
            (ItemType)request.ItemType, request.Code, request.NameNepali,
            request.Barcode, request.Description, request.CategoryId,
            request.ReorderLevel, request.ReorderQuantity, request.StandardCost,
            request.SalePrice, request.AllowNegativeStock, (CostingMethod)request.CostingMethod);

        await _unitOfWork.Items.AddAsync(item);
        await _unitOfWork.SaveChangesAsync();

        return MapItemToDto(item);
    }

    public async Task RecordStockMovementAsync(CreateStockMovementRequest request, Guid userId)
    {
        if (request.MovementDate == default)
            throw new InvalidOperationException("Stock movement date is required.");
        if (request.Quantity <= 0)
            throw new InvalidOperationException("Stock movement quantity must be greater than zero.");
        if (request.UnitCost < 0)
            throw new InvalidOperationException("Stock movement unit cost cannot be negative.");
        if (!Enum.IsDefined(typeof(MovementType), request.MovementType))
            throw new InvalidOperationException("Stock movement type is invalid.");
        var movementType = (MovementType)request.MovementType;
        if (movementType is MovementType.StockTransferIn or MovementType.StockTransferOut)
            throw new InvalidOperationException("Standalone stock transfers are not supported; use an atomic warehouse transfer.");
        var movementDate = request.MovementDate.Date;

        var item = await _unitOfWork.Items.GetByIdWithDetailsAsync(request.ItemId)
            ?? throw new InvalidOperationException("Item not found.");
        if (item.CompanyId != request.CompanyId)
            throw new InvalidOperationException("Item does not belong to the selected company.");
        if (!item.IsActive || item.ItemType != ItemType.Goods)
            throw new InvalidOperationException("Stock movements require an active goods item.");
        if (item.AllowNegativeStock)
            throw new InvalidOperationException("Negative inventory is not supported with weighted-average costing.");
        await EnsureWarehouseBelongsToCompanyAsync(request.CompanyId, request.WarehouseId);


        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _posting.EnsurePeriodOpenAsync(request.CompanyId, movementDate);

            var balance = await _unitOfWork.StockBalances.GetByItemWarehouseAsync(request.ItemId, request.WarehouseId);

            if (balance == null)
            {
                balance = StockBalance.Create(request.CompanyId, request.ItemId, request.WarehouseId);
                await _unitOfWork.StockBalances.AddAsync(balance);
            }
            else if (balance.CompanyId != request.CompanyId)
                throw new InvalidOperationException("Stock balance does not belong to the selected company.");

            var prevAverageCost = balance.AverageCost;
            var isOutbound = movementType is MovementType.SaleOut or MovementType.PurchaseReturn or MovementType.Damage or MovementType.WriteOff;
            var unitCost = isOutbound ? prevAverageCost : request.UnitCost > 0 ? request.UnitCost : prevAverageCost;

            switch (movementType)
            {
                case MovementType.PurchaseIn:
                case MovementType.SalesReturn:
                case MovementType.StockTransferIn:
                case MovementType.OpeningStock:
                    balance.AddStock(request.Quantity, unitCost);
                    break;

                case MovementType.SaleOut:
                case MovementType.PurchaseReturn:
                case MovementType.StockTransferOut:
                case MovementType.Damage:
                case MovementType.WriteOff:
                    if (!item.AllowNegativeStock && balance.Quantity < request.Quantity)
                        throw new InvalidOperationException($"Insufficient stock for {item.Name}. Available: {balance.Quantity}, Required: {request.Quantity}");
                    balance.RemoveStock(request.Quantity, unitCost, item.AllowNegativeStock);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported movement type: {movementType}");
            }

            var movement = StockMovement.Create(
                request.CompanyId, request.ItemId, request.WarehouseId,
                movementType, request.Quantity, unitCost,
                balance.Quantity, balance.TotalValue, movementDate,
                narration: request.Narration,
                batchNumber: request.BatchNumber,
                expiryDate: request.ExpiryDate);

            await _unitOfWork.StockMovements.AddAsync(movement);
            await _unitOfWork.SaveChangesAsync();
            await PostStockMovementJournalAsync(request.CompanyId, movementDate, movementType, request.Quantity * unitCost, request.Narration, userId);
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task AdjustStockAsync(CreateStockAdjustmentRequest request, Guid userId)
    {
        if (request.AdjustmentDate == default)
            throw new InvalidOperationException("Stock adjustment date is required.");
        var adjustmentDate = request.AdjustmentDate.Date;
        var item = await _unitOfWork.Items.GetByIdWithDetailsAsync(request.ItemId)
            ?? throw new InvalidOperationException("Item not found.");
        if (item.CompanyId != request.CompanyId)
            throw new InvalidOperationException("Item does not belong to the selected company.");
        if (!item.IsActive || item.ItemType != ItemType.Goods)
            throw new InvalidOperationException("Stock adjustments require an active goods item.");
        if (item.AllowNegativeStock)
            throw new InvalidOperationException("Negative inventory is not supported with weighted-average costing.");
        if (request.AdjustedQuantity < 0)
            throw new InvalidOperationException("Adjusted quantity cannot be negative.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("A stock adjustment reason is required.");
        await EnsureWarehouseBelongsToCompanyAsync(request.CompanyId, request.WarehouseId);


        var balance = await _unitOfWork.StockBalances.GetByItemWarehouseAsync(request.ItemId, request.WarehouseId);
        if (balance == null)
            throw new InvalidOperationException("No stock balance found for this item and warehouse.");
        if (balance.CompanyId != request.CompanyId)
            throw new InvalidOperationException("Stock balance does not belong to the selected company.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _posting.EnsurePeriodOpenAsync(request.CompanyId, adjustmentDate);

            var previousValue = balance.TotalValue;
            var difference = request.AdjustedQuantity - balance.Quantity;

            balance.AdjustStock(request.AdjustedQuantity, balance.AverageCost);

            var movement = StockMovement.Create(
                request.CompanyId, request.ItemId, request.WarehouseId,
                MovementType.StockAdjustment, Math.Abs(difference), balance.AverageCost,
                balance.Quantity, balance.TotalValue, adjustmentDate,
                narration: $"Stock adjustment: {request.Reason}. {request.Narration}");

            await _unitOfWork.StockMovements.AddAsync(movement);
            await _unitOfWork.SaveChangesAsync();
            await PostStockAdjustmentJournalAsync(
                request.CompanyId,
                adjustmentDate,
                previousValue,
                balance.TotalValue,
                $"Stock adjustment: {request.Reason}. {request.Narration}",
                userId);
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<StockBalanceDto>> GetStockBalancesAsync(Guid companyId, Guid? warehouseId = null)
    {
        var items = await _unitOfWork.Items.GetByCompanyAsync(companyId);

        var balances = items.SelectMany(i => i.StockBalances)
            .Where(sb => !warehouseId.HasValue || sb.WarehouseId == warehouseId.Value)
            .Select(sb => new StockBalanceDto
            {
                ItemId = sb.ItemId,
                ItemName = sb.Item.Name,
                WarehouseId = sb.WarehouseId,
                WarehouseName = sb.Warehouse.Name,
                Quantity = sb.Quantity,
                AverageCost = sb.AverageCost,
                TotalValue = sb.TotalValue
            }).ToList();

        return balances;
    }

    public async Task<IReadOnlyList<InventoryReportDto>> GetInventoryReportAsync(Guid companyId, Guid? warehouseId = null)
    {
        var items = await _unitOfWork.Items.GetByCompanyAsync(companyId);

        var report = items.Where(i => i.ItemType == ItemType.Goods).SelectMany(i =>
            i.StockBalances.Where(sb => !warehouseId.HasValue || sb.WarehouseId == warehouseId.Value)
             .Select(sb => new InventoryReportDto
             {
                 ItemId = i.Id,
                 ItemName = i.Name,
                 CategoryName = i.Category?.Name,
                 UnitName = i.Unit.Name,
                 WarehouseName = sb.Warehouse.Name,
                 Quantity = sb.Quantity,
                 AverageCost = sb.AverageCost,
                 TotalValue = sb.TotalValue,
                 ReorderLevel = i.ReorderLevel,
                 IsLowStock = i.IsLowStock
             })).ToList();

        return report;
    }

    public async Task<IReadOnlyList<ItemDto>> GetLowStockItemsAsync(Guid companyId)
    {
        var items = await _unitOfWork.Items.GetLowStockItemsAsync(companyId);
        return items.Select(MapItemToDto).ToList();
    }

    private static ItemDto MapItemToDto(Item item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Code = item.Code,
        Barcode = item.Barcode,
        ItemType = (ItemTypeDto)item.ItemType,
        CategoryName = item.Category?.Name,
        UnitName = item.Unit.Name,
        ReorderLevel = item.ReorderLevel,
        SalePrice = item.SalePrice,
        StandardCost = item.StandardCost,
        IsActive = item.IsActive,
        IsLowStock = item.IsLowStock
    };

    private async Task EnsureWarehouseBelongsToCompanyAsync(Guid companyId, Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
            throw new InvalidOperationException("Warehouse is required.");

        var warehouses = await _unitOfWork.Warehouses.GetByCompanyAsync(companyId);
        if (warehouses.All(w => w.Id != warehouseId || !w.IsActive))
            throw new InvalidOperationException("Warehouse does not belong to the selected company or is inactive.");
    }

    private async Task PostStockMovementJournalAsync(Guid companyId, DateTime movementDate, MovementType movementType, decimal amount, string? narration, Guid userId)
    {
        if (amount <= 0 || movementType is MovementType.StockTransferIn or MovementType.StockTransferOut)
            return;

        var inventoryLedger = await _posting.GetOrCreateInventoryLedgerAsync(companyId);
        var entryNarration = narration ?? $"Stock movement: {movementType}";
        var lines = new List<PostingLine>();

        switch (movementType)
        {
            case MovementType.OpeningStock:
                var openingLedger = await _posting.GetOrCreateOpeningBalanceLedgerAsync(companyId);
                lines.Add(new PostingLine(inventoryLedger.Id, amount, 0, entryNarration));
                lines.Add(new PostingLine(openingLedger.Id, 0, amount, entryNarration));
                break;

            case MovementType.PurchaseIn:
            case MovementType.SalesReturn:
                var cogsLedger = await _posting.GetOrCreateCostOfSalesLedgerAsync(companyId);
                lines.Add(new PostingLine(inventoryLedger.Id, amount, 0, entryNarration));
                lines.Add(new PostingLine(cogsLedger.Id, 0, amount, entryNarration));
                break;

            case MovementType.SaleOut:
                cogsLedger = await _posting.GetOrCreateCostOfSalesLedgerAsync(companyId);
                lines.Add(new PostingLine(cogsLedger.Id, amount, 0, entryNarration));
                lines.Add(new PostingLine(inventoryLedger.Id, 0, amount, entryNarration));
                break;

            case MovementType.PurchaseReturn:
                cogsLedger = await _posting.GetOrCreateCostOfSalesLedgerAsync(companyId);
                lines.Add(new PostingLine(cogsLedger.Id, amount, 0, entryNarration));
                lines.Add(new PostingLine(inventoryLedger.Id, 0, amount, entryNarration));
                break;

            case MovementType.Damage:
            case MovementType.WriteOff:
                var writeOffLedger = await _posting.GetOrCreateInventoryWriteOffLedgerAsync(companyId);
                lines.Add(new PostingLine(writeOffLedger.Id, amount, 0, entryNarration));
                lines.Add(new PostingLine(inventoryLedger.Id, 0, amount, entryNarration));
                break;
        }

        if (lines.Count > 0)
        {
            await _posting.CreateAndPostJournalAsync(
                companyId,
                movementDate,
                VoucherType.StockJournal,
                userId,
                null,
                entryNarration,
                null,
                lines);
        }
    }

    private async Task PostStockAdjustmentJournalAsync(Guid companyId, DateTime adjustmentDate, decimal previousValue, decimal adjustedValue, string narration, Guid userId)
    {
        var difference = adjustedValue - previousValue;
        if (difference == 0)
            return;

        var inventoryLedger = await _posting.GetOrCreateInventoryLedgerAsync(companyId);
        var adjustmentLedger = await _posting.GetOrCreateStockAdjustmentLedgerAsync(companyId);
        var amount = Math.Abs(difference);

        var lines = difference > 0
            ? new[]
            {
                new PostingLine(inventoryLedger.Id, amount, 0, narration),
                new PostingLine(adjustmentLedger.Id, 0, amount, narration)
            }
            : new[]
            {
                new PostingLine(adjustmentLedger.Id, amount, 0, narration),
                new PostingLine(inventoryLedger.Id, 0, amount, narration)
            };

        await _posting.CreateAndPostJournalAsync(
            companyId,
            adjustmentDate,
            VoucherType.StockJournal,
            userId,
            null,
            narration,
            null,
            lines);
    }
}
