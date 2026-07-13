using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Requests;
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
    private string _searchText = "";

    [ObservableProperty]
    private ObservableCollection<string> _availableItems = new();

    [ObservableProperty]
    private string _itemName = "";

    [ObservableProperty]
    private string _itemCode = "";

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

    public string[] MovementTypes { get; } =
    {
        "Opening Stock",
        "Purchase In",
        "Sale Out",
        "Purchase Return",
        "Sales Return",
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

            var unit = await _dataService.GetOrCreateUnitAsync(_session.CompanyId.Value);
            var item = await _inventoryService.CreateItemAsync(new CreateItemRequest
            {
                CompanyId = _session.CompanyId.Value,
                Name = ItemName.Trim(),
                Code = string.IsNullOrWhiteSpace(ItemCode) ? null : ItemCode.Trim(),
                UnitId = unit.Id,
                StandardCost = StandardCost,
                SalePrice = SalePrice,
                ReorderLevel = ReorderLevel > 0 ? ReorderLevel : null,
                ItemType = 1,
                CostingMethod = 2
            }, _session.UserId);

            StatusMessage = $"Saved item {item.Name}.";
            MovementItemName = item.Name;
            ItemName = "";
            ItemCode = "";
            StandardCost = 0;
            SalePrice = 0;
            ReorderLevel = 0;
            await LoadItemsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
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

            var warehouse = await _dataService.GetOrCreateWarehouseAsync(_session.CompanyId.Value);
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
    private void Search() => ApplyFilter();

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
            var allItems = new List<ItemListItemViewModel>();
            var balances = await _inventoryService.GetInventoryReportAsync(_session.CompanyId.Value);
            foreach (var item in balances.OrderBy(i => i.ItemName))
            {
                allItems.Add(new ItemListItemViewModel
                {
                    Name = item.ItemName,
                    Warehouse = item.WarehouseName ?? "",
                    Quantity = item.Quantity,
                    AverageCost = item.AverageCost,
                    TotalValue = item.TotalValue,
                    IsLowStock = item.IsLowStock
                });
            }

            var rawItems = await _dataService.GetItemsAsync(_session.CompanyId.Value);
            foreach (var item in rawItems.Where(i => !allItems.Any(x => x.Name == i.Name)).OrderBy(i => i.Name))
            {
                allItems.Add(new ItemListItemViewModel
                {
                    Name = item.Name,
                    Warehouse = "",
                    Quantity = 0,
                    AverageCost = item.StandardCost,
                    TotalValue = 0,
                    IsLowStock = item.IsLowStock
                });
            }

            _allItems = allItems;
            AvailableItems = new ObservableCollection<string>(rawItems.Where(i => i.IsActive).OrderBy(i => i.Name).Select(i => i.Name));
            ApplyFilter();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private static int MovementTypeToInt(string value) => value switch
    {
        "Purchase In" => 2,
        "Sale Out" => 3,
        "Purchase Return" => 4,
        "Sales Return" => 5,
        "Damage" => 9,
        "Write Off" => 10,
        _ => 1
    };
}

public partial class ItemListItemViewModel : ObservableObject
{
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
}
