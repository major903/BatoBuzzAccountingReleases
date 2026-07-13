using BatoBuzz.Application.Interfaces;
using BatoBuzz.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BatoBuzz.Desktop.ViewModels;

public partial class PeriodLockSettingsViewModel : ObservableObject
{
    private readonly ICompanyService _companyService;
    private readonly DesktopSession _session;

    [ObservableProperty]
    private DateTime? _currentLockDate;

    [ObservableProperty]
    private DateTime? _lockDate;

    [ObservableProperty]
    private string _statusMessage = "";

    public PeriodLockSettingsViewModel(ICompanyService companyService, DesktopSession session)
    {
        _companyService = companyService;
        _session = session;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (!_session.CompanyId.HasValue)
            return;

        try
        {
            var company = await _companyService.GetCompanyAsync(_session.CompanyId.Value);
            CurrentLockDate = company?.PeriodLockDate;
            LockDate = CurrentLockDate;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
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
            var company = await _companyService.SetPeriodLockDateAsync(_session.CompanyId.Value, LockDate, _session.UserId);
            CurrentLockDate = company.PeriodLockDate;
            StatusMessage = CurrentLockDate.HasValue
                ? $"Period locked on or before {CurrentLockDate.Value:yyyy-MM-dd}."
                : "Period lock cleared.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task Clear()
    {
        LockDate = null;
        await Save();
    }
}
