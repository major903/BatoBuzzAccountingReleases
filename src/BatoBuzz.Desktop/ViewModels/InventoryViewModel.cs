using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Contracts.Responses;
using BatoBuzz.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace BatoBuzz.Desktop.ViewModels;

public partial class InventoryViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly DesktopDataService _dataService;
    private readonly DesktopSession _session;
    private List<ItemListItemViewModel> _allItems = new();

    [ObservableProperty]
    private ObservableCollection<ItemListItemViewModel> _items = new();

    [ObservableProperty]
    private ItemListItemViewModel? _selectedItem;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private ObservableCollection<string> _availableItems = new();

    [ObservableProperty]
    private string _itemName = "";

    [ObservableProperty]
    private string _itemCode = "";

    [ObservableProperty]
    private ObservableCollection<BatoBuzz.Domain.Entities.Unit> _units = new();

    [ObservableProperty]
    private BatoBuzz.Domain.Entities.Unit? _selectedUnit;

    [ObservableProperty]
    private ObservableCollection<BatoBuzz.Domain.Entities.ItemCategory> _categories = new();

    [ObservableProperty]
    private BatoBuzz.Domain.Entities.ItemCategory? _selectedCategory;

    [ObservableProperty]
    private ObservableCollection<BatoBuzz.Domain.Entities.Warehouse> _warehouses = new();

    [ObservableProperty]
    private BatoBuzz.Domain.Entities.Warehouse? _selectedWarehouse;

    [ObservableProperty]
    private string _newUnitName = "";

    [ObservableProperty]
    private string _newWarehouseName = "";

    [ObservableProperty]
    private string _newCategoryName = "";

    [ObservableProperty]
    private decimal _standardCost;

    [ObservableProperty]
    private decimal _salePrice;

    [ObservableProperty]
    private decimal _reorderLevel;

    [ObservableProperty]
    private string _movementItemName = "";

    [ObservableProperty]
    private string _movementType = "Opening Stock";

    [ObservableProperty]
    private DateTime _movementDate = DateTime.Today;

    [ObservableProperty]
    private decimal _quantity;

    [ObservableProperty]
    private decimal _unitCost;

    [ObservableProperty]
    private string _narration = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private ObservableCollection<StockMovementDto> _stockMovements = new();

    [ObservableProperty]
    private StockMovementDto? _selectedStockMovement;

    [ObservableProperty]
    private DateTime _correctionDate = DateTime.Today;

    [ObservableProperty]
    private string _correctionReason = "";

    public string[] MovementTypes { get; } =
    {
        "Opening Stock",
        "Damage",
        "Write Off"
    };

    public InventoryViewModel(IInventoryService inventoryService, DesktopDataService dataService, DesktopSession session)
    {
        _inventoryService = inventoryService;
        _dataService = dataService;
        _session = session;
        _ = LoadItemsAsync();
    }

    [RelayCommand]
    private async Task SaveItem()
    {
        if (!_session.CompanyId.HasValue)
        {
            StatusMessage = "No company selected.";
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(ItemName))
                throw new InvalidOperationException("Item name is required.");

            var item = SelectedItem == null
                ? await CreateItemAsync()
                : await _inventoryService.UpdateItemAsync(
                    SelectedItem.Id, ItemName, ItemCode, StandardCost, SalePrice,
                    ReorderLevel > 0 ? ReorderLevel : null, true, _session.UserId);

            StatusMessage = $"Saved item {item.Name}.";
            MovementItemName = item.Name;
            ItemName = "";
            ItemCode = "";
            StandardCost = 0;
            SalePrice = 0;
            ReorderLevel = 0;
            SelectedItem = null;
            await LoadItemsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task<ItemDto> CreateItemAsync()
    {
        var unit = SelectedUnit ?? await _dataService.GetOrCreateUnitAsync(_session.CompanyId!.Value);
        return await _inventoryService.CreateItemAsync(new CreateItemRequest
        {
            CompanyId = _session.CompanyId!.Value,
            Name = ItemName.Trim(),
            Code = string.IsNullOrWhiteSpace(ItemCode) ? null : ItemCode.Trim(),
            UnitId = unit.Id,
            CategoryId = SelectedCategory?.Id,
            StandardCost = StandardCost,
            SalePrice = SalePrice,
            ReorderLevel = ReorderLevel > 0 ? ReorderLevel : null,
            ItemType = 1,
            CostingMethod = 2
        }, _session.UserId);
    }

    [RelayCommand]
    private async Task RecordMovement()
    {
        if (!_session.CompanyId.HasValue)
        {
            StatusMessage = "No company selected.";
            return;
        }

        try
        {
            if (Quantity <= 0)
                throw new InvalidOperationException("Quantity must be greater than zero.");

            var confirmResult = MessageBox.Show($"Are you sure you want to post this stock movement of {Quantity} units for '{MovementItemName}'?", "Confirm Stock Movement", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmResult != MessageBoxResult.Yes)
                return;

            var item = await _dataService.GetItemByNameAsync(_session.CompanyId.Value, MovementItemName);
            if (item == null)
                throw new InvalidOperationException("Item not found.");

            var warehouse = SelectedWarehouse ?? await _dataService.GetOrCreateWarehouseAsync(_session.CompanyId.Value);
            await _inventoryService.RecordStockMovementAsync(new CreateStockMovementRequest
            {
                CompanyId = _session.CompanyId.Value,
                ItemId = item.Id,
                WarehouseId = warehouse.Id,
                MovementDate = MovementDate.Date,
                MovementType = MovementTypeToInt(MovementType),
                Quantity = Quantity,
                UnitCost = UnitCost > 0 ? UnitCost : item.StandardCost,
                Narration = string.IsNullOrWhiteSpace(Narration) ? null : Narration.Trim()
            }, _session.UserId);

            StatusMessage = "Stock movement posted.";
            Quantity = 0;
            UnitCost = 0;
            Narration = "";
            await LoadItemsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ReverseSelectedMovement()
    {
        if (SelectedStockMovement == null)
        {
            StatusMessage = "Select a stock movement to reverse.";
            return;
        }
        if (!SelectedStockMovement.CanReverse)
        {
            StatusMessage = "This stock movement cannot be reversed from the application.";
            return;
        }
        if (string.IsNullOrWhiteSpace(CorrectionReason))
        {
            StatusMessage = "A correction reason is required.";
            return;
        }
        if (MessageBox.Show(
                $"Reverse the selected stock movement for {SelectedStockMovement.ItemName}? This posts a dated reversal journal and stock movement.",
                "Reverse Stock Movement", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            await _inventoryService.ReverseStockMovementAsync(
                SelectedStockMovement.Id,
                new CorrectPostedDocumentRequest
                {
                    CorrectionDate = CorrectionDate,
                    Reason = CorrectionReason.Trim()
                },
                _session.UserId);
            CorrectionReason = "";
            StatusMessage = "Stock movement reversed.";
            await LoadItemsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Search() => ApplyFilter();

    [RelayCommand]
    private async Task AddUnit()
    {
        if (!_session.CompanyId.HasValue)
            return;
        try
        {
            await _dataService.GetOrCreateUnitAsync(_session.CompanyId.Value, NewUnitName);
            NewUnitName = "";
            await LoadMastersAsync();
            StatusMessage = "Unit saved.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task AddWarehouse()
    {
        if (!_session.CompanyId.HasValue)
            return;
        try
        {
            await _dataService.GetOrCreateWarehouseAsync(_session.CompanyId.Value, NewWarehouseName);
            NewWarehouseName = "";
            await LoadMastersAsync();
            StatusMessage = "Warehouse saved.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task AddCategory()
    {
        if (!_session.CompanyId.HasValue)
            return;
        try
        {
            await _dataService.GetOrCreateItemCategoryAsync(_session.CompanyId.Value, NewCategoryName);
            NewCategoryName = "";
            await LoadMastersAsync();
            StatusMessage = "Category saved.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeactivateSelectedUnit()
    {
        if (SelectedUnit == null) { StatusMessage = "Select a unit first."; return; }
        try
        {
            await _dataService.SetUnitActiveAsync(SelectedUnit.Id, false, _session.UserId);
            StatusMessage = $"Deactivated unit {SelectedUnit.Name}.";
            await LoadMastersAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeactivateSelectedWarehouse()
    {
        if (SelectedWarehouse == null) { StatusMessage = "Select a warehouse first."; return; }
        try
        {
            await _dataService.SetWarehouseActiveAsync(SelectedWarehouse.Id, false, _session.UserId);
            StatusMessage = $"Deactivated warehouse {SelectedWarehouse.Name}.";
            await LoadMastersAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeactivateSelectedCategory()
    {
        if (SelectedCategory == null) { StatusMessage = "Select a category first."; return; }
        try
        {
            await _dataService.SetItemCategoryActiveAsync(SelectedCategory.Id, false, _session.UserId);
            StatusMessage = $"Deactivated category {SelectedCategory.Name}.";
            await LoadMastersAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeactivateSelectedItem()
    {
        if (SelectedItem == null) { StatusMessage = "Select an item first."; return; }
        try
        {
            await _inventoryService.UpdateItemAsync(
                SelectedItem.Id, SelectedItem.Name, SelectedItem.Code, SelectedItem.StandardCost,
                SelectedItem.SalePrice, SelectedItem.ReorderLevel, false, _session.UserId);
            StatusMessage = $"Deactivated item {SelectedItem.Name}.";
            SelectedItem = null;
            await LoadItemsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    partial void OnSelectedItemChanged(ItemListItemViewModel? value)
    {
        if (value == null)
            return;
        ItemName = value.Name;
        ItemCode = value.Code ?? "";
        StandardCost = value.StandardCost;
        SalePrice = value.SalePrice;
        ReorderLevel = value.ReorderLevel ?? 0;
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allItems
            : _allItems.Where(i => i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        Items = new ObservableCollection<ItemListItemViewModel>(filtered);
    }

    private async Task LoadItemsAsync()
    {
        if (!_session.CompanyId.HasValue)
            return;

        try
        {
            await LoadMastersAsync();
            var rawItems = await _dataService.GetItemsAsync(_session.CompanyId.Value);
            var itemsById = rawItems.ToDictionary(item => item.Id);
            var allItems = new List<ItemListItemViewModel>();
            var balances = await _inventoryService.GetInventoryReportAsync(_session.CompanyId.Value);
            foreach (var item in balances.OrderBy(i => i.ItemName))
            {
                allItems.Add(new ItemListItemViewModel
                {
                    Id = item.ItemId,
                    Name = item.ItemName,
                    Warehouse = item.WarehouseName ?? "",
                    Quantity = item.Quantity,
                    AverageCost = item.AverageCost,
                    TotalValue = item.TotalValue,
                    IsLowStock = item.IsLowStock,
                    Code = itemsById.GetValueOrDefault(item.ItemId)?.Code,
                    StandardCost = itemsById.GetValueOrDefault(item.ItemId)?.StandardCost ?? item.AverageCost,
                    SalePrice = itemsById.GetValueOrDefault(item.ItemId)?.SalePrice ?? 0,
                    ReorderLevel = itemsById.GetValueOrDefault(item.ItemId)?.ReorderLevel
                });
            }

            foreach (var item in rawItems.Where(i => !allItems.Any(x => x.Id == i.Id)).OrderBy(i => i.Name))
            {
                allItems.Add(new ItemListItemViewModel
                {
                    Id = item.Id,
                    Name = item.Name,
                    Warehouse = "",
                    Quantity = 0,
                    AverageCost = item.StandardCost,
                    TotalValue = 0,
                    IsLowStock = item.IsLowStock,
                    Code = item.Code,
                    StandardCost = item.StandardCost,
                    SalePrice = item.SalePrice,
                    ReorderLevel = item.ReorderLevel
                });
            }

            _allItems = allItems;
            AvailableItems = new ObservableCollection<string>(rawItems.Where(i => i.IsActive).OrderBy(i => i.Name).Select(i => i.Name));
            StockMovements = new ObservableCollection<StockMovementDto>(
                await _inventoryService.GetStockMovementsAsync(_session.CompanyId.Value));
            ApplyFilter();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task LoadMastersAsync()
    {
        if (!_session.CompanyId.HasValue)
            return;

        var companyId = _session.CompanyId.Value;
        var selectedUnitId = SelectedUnit?.Id;
        var selectedCategoryId = SelectedCategory?.Id;
        var selectedWarehouseId = SelectedWarehouse?.Id;
        Units = new ObservableCollection<BatoBuzz.Domain.Entities.Unit>(
            (await _dataService.GetUnitsAsync(companyId)).Where(unit => unit.IsActive).OrderBy(unit => unit.Name));
        Categories = new ObservableCollection<BatoBuzz.Domain.Entities.ItemCategory>(
            (await _dataService.GetItemCategoriesAsync(companyId)).Where(category => category.IsActive).OrderBy(category => category.Name));
        Warehouses = new ObservableCollection<BatoBuzz.Domain.Entities.Warehouse>(
            (await _dataService.GetWarehousesAsync(companyId)).Where(warehouse => warehouse.IsActive).OrderBy(warehouse => warehouse.Name));
        SelectedUnit = Units.FirstOrDefault(unit => unit.Id == selectedUnitId) ?? Units.FirstOrDefault();
        SelectedCategory = Categories.FirstOrDefault(category => category.Id == selectedCategoryId);
        SelectedWarehouse = Warehouses.FirstOrDefault(warehouse => warehouse.Id == selectedWarehouseId)
            ?? Warehouses.FirstOrDefault(warehouse => warehouse.IsDefault)
            ?? Warehouses.FirstOrDefault();
    }

    private static int MovementTypeToInt(string value) => value switch
    {
        "Damage" => 9,
        "Write Off" => 10,
        _ => 1
    };
}

public partial class ItemListItemViewModel : ObservableObject
{
    public Guid Id { get; init; }

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _warehouse = "";

    [ObservableProperty]
    private decimal _quantity;

    [ObservableProperty]
    private decimal _averageCost;

    [ObservableProperty]
    private decimal _totalValue;

    [ObservableProperty]
    private bool _isLowStock;

    public string? Code { get; init; }
    public decimal StandardCost { get; init; }
    public decimal SalePrice { get; init; }
    public decimal? ReorderLevel { get; init; }
}
