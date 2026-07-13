using BatoBuzz.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BatoBuzz.Desktop.ViewModels;

public partial class SupplierViewModel : ObservableObject
{
    private readonly DesktopDataService _dataService;
    private readonly DesktopSession _session;

    [ObservableProperty]
    private ObservableCollection<SupplierListItemViewModel> _suppliers = new();

    [ObservableProperty]
    private SupplierListItemViewModel? _selectedSupplier;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _address = "";

    [ObservableProperty]
    private string _city = "";

    [ObservableProperty]
    private string _phone = "";

    [ObservableProperty]
    private string _email = "";

    [ObservableProperty]
    private string _panNumber = "";

    [ObservableProperty]
    private decimal _creditLimit;

    [ObservableProperty]
    private string _statusMessage = "";

    public SupplierViewModel(DesktopDataService dataService, DesktopSession session)
    {
        _dataService = dataService;
        _session = session;
        _ = LoadSuppliersAsync();
    }

    [RelayCommand]
    private async Task Search() => await LoadSuppliersAsync();

    [RelayCommand]
    private void AddNew()
    {
        IsEditing = true;
        SelectedSupplier = null;
        Name = "";
        Address = "";
        City = "";
        Phone = "";
        Email = "";
        PanNumber = "";
        CreditLimit = 0;
        StatusMessage = "";
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedSupplier == null)
            return;

        IsEditing = true;
        Name = SelectedSupplier.Name;
        Address = SelectedSupplier.Address;
        City = SelectedSupplier.City;
        Phone = SelectedSupplier.Phone;
        Email = SelectedSupplier.Email;
        PanNumber = SelectedSupplier.PanNumber;
        CreditLimit = SelectedSupplier.CreditLimit;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (!_session.CompanyId.HasValue)
        {
            StatusMessage = "No company selected.";
            return;
        }

        try
        {
            if (SelectedSupplier != null)
            {
                await _dataService.UpdateSupplierAsync(
                    SelectedSupplier.Id,
                    Name,
                    Address,
                    City,
                    Phone,
                    Email,
                    PanNumber,
                    CreditLimit,
                    _session.UserId);
            }
            else
            {
                await _dataService.CreateSupplierAsync(
                    _session.CompanyId.Value,
                    Name,
                    Address,
                    City,
                    Phone,
                    Email,
                    PanNumber,
                    CreditLimit,
                    _session.UserId);
            }

            IsEditing = false;
            StatusMessage = "Supplier saved.";
            await LoadSuppliersAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel() => IsEditing = false;

    private async Task LoadSuppliersAsync()
    {
        if (!_session.CompanyId.HasValue)
            return;

        try
        {
            var suppliers = await _dataService.GetSuppliersAsync(_session.CompanyId.Value, SearchText);
            Suppliers.Clear();
            foreach (var s in suppliers.OrderBy(s => s.Name))
            {
                Suppliers.Add(new SupplierListItemViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    City = s.City ?? "",
                    Address = s.Address ?? "",
                    Phone = s.Phone ?? "",
                    Email = s.Email ?? "",
                    PanNumber = s.PanNumber ?? "",
                    CreditLimit = s.CreditLimit,
                    Balance = s.CurrentBalance
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}

public partial class SupplierListItemViewModel : ObservableObject
{
    public Guid Id { get; set; }

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _city = "";

    [ObservableProperty]
    private string _address = "";

    [ObservableProperty]
    private string _phone = "";

    [ObservableProperty]
    private string _email = "";

    [ObservableProperty]
    private string _panNumber = "";

    [ObservableProperty]
    private decimal _creditLimit;

    [ObservableProperty]
    private decimal _balance;
}
