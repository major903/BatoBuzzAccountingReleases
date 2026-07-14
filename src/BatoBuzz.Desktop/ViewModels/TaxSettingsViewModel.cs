using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BatoBuzz.Desktop.ViewModels;

public partial class TaxSettingsViewModel : ObservableObject
{
    private readonly ITdsService _tdsService;
    private readonly DesktopSession _session;

    [ObservableProperty]
    private ObservableCollection<TdsRateListItemViewModel> _rates = new();

    [ObservableProperty]
    private TdsRateListItemViewModel? _selectedRate;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private decimal _ratePercent;

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string _statusMessage = "";

    public TaxSettingsViewModel(ITdsService tdsService, DesktopSession session)
    {
        _tdsService = tdsService;
        _session = session;
        _ = LoadRatesAsync();
    }

    [RelayCommand]
    private void AddNew()
    {
        IsEditing = true;
        SelectedRate = null;
        Name = "";
        RatePercent = 0;
        Description = "";
        IsActive = true;
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedRate == null)
            return;

        IsEditing = true;
        Name = SelectedRate.Name;
        RatePercent = SelectedRate.RatePercent;
        Description = SelectedRate.Description;
        IsActive = SelectedRate.IsActive;
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
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("Rate name is required.");

            if (SelectedRate != null)
            {
                await _tdsService.UpdateRateAsync(SelectedRate.Id, Name, RatePercent, Description, IsActive);
            }
            else
            {
                await _tdsService.CreateRateAsync(new CreateTdsRateRequest
                {
                    CompanyId = _session.CompanyId.Value,
                    Name = Name.Trim(),
                    RatePercent = RatePercent,
                    Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim()
                }, _session.UserId);
            }

            IsEditing = false;
            StatusMessage = "TDS rate saved.";
            await LoadRatesAsync();
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

    private async Task LoadRatesAsync()
    {
        if (!_session.CompanyId.HasValue)
            return;

        try
        {
            var rates = await _tdsService.GetRatesAsync(_session.CompanyId.Value, activeOnly: false);
            Rates.Clear();
            foreach (var r in rates)
            {
                Rates.Add(new TdsRateListItemViewModel
                {
                    Id = r.Id,
                    Name = r.Name,
                    RatePercent = r.RatePercent,
                    Description = r.Description ?? "",
                    IsActive = r.IsActive
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}

public partial class TdsRateListItemViewModel : ObservableObject
{
    public Guid Id { get; set; }

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private decimal _ratePercent;

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private bool _isActive;
}
