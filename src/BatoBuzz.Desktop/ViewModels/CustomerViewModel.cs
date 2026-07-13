using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BatoBuzz.Desktop.Services;
using System.Collections.ObjectModel;

namespace BatoBuzz.Desktop.ViewModels;

public partial class CustomerViewModel : ObservableObject
{
    private readonly DesktopDataService _dataService;
    private readonly DesktopSession _session;

    [ObservableProperty]
    private ObservableCollection<CustomerListItemViewModel> _customers = new();

    [ObservableProperty]
    private CustomerListItemViewModel? _selectedCustomer;

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

    public CustomerViewModel(DesktopDataService dataService, DesktopSession session)
    {
        _dataService = dataService;
        _session = session;
        _ = LoadCustomersAsync();
    }

    [RelayCommand]
    private async Task Search()
    {
        await LoadCustomersAsync();
    }

    [RelayCommand]
    private void AddNew()
    {
        IsEditing = true;
        SelectedCustomer = null;
        Name = "";
        Address = "";
        City = "";
        Phone = "";
        Email = "";
        PanNumber = "";
        CreditLimit = 0;
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedCustomer != null)
        {
            IsEditing = true;
            Name = SelectedCustomer.Name;
            Address = SelectedCustomer.Address;
            City = SelectedCustomer.City;
            Phone = SelectedCustomer.Phone;
            Email = SelectedCustomer.Email;
            PanNumber = SelectedCustomer.PanNumber;
            CreditLimit = SelectedCustomer.CreditLimit;
        }
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
            if (SelectedCustomer != null)
            {
                await _dataService.UpdateCustomerAsync(
                    SelectedCustomer.Id,
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
                await _dataService.CreateCustomerAsync(
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
            StatusMessage = "Customer saved.";
            await LoadCustomersAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        IsEditing = false;
    }

    private async Task LoadCustomersAsync()
    {
        if (!_session.CompanyId.HasValue)
            return;

        try
        {
            var customers = await _dataService.GetCustomersAsync(_session.CompanyId.Value, SearchText);
            Customers.Clear();
            foreach (var c in customers.OrderBy(c => c.Name))
            {
                Customers.Add(new CustomerListItemViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    City = c.City ?? "",
                    Address = c.Address ?? "",
                    Phone = c.Phone ?? "",
                    Email = c.Email ?? "",
                    PanNumber = c.PanNumber ?? "",
                    CreditLimit = c.CreditLimit,
                    Balance = c.CurrentBalance
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}

public partial class CustomerListItemViewModel : ObservableObject
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
