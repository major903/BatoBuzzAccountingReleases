using BatoBuzz.Application.Interfaces;
using BatoBuzz.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BatoBuzz.Desktop.ViewModels;

public partial class ChangeCompanyViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private readonly ICompanyService _companyService;
    private readonly DesktopSession _session;

    [ObservableProperty]
    private ObservableCollection<CompanyListItemViewModel> _companies = new();

    [ObservableProperty]
    private string _statusMessage = "";

    public ChangeCompanyViewModel(MainViewModel mainViewModel, ICompanyService companyService, DesktopSession session)
    {
        _mainViewModel = mainViewModel;
        _companyService = companyService;
        _session = session;
        _ = LoadCompaniesAsync();
    }

    private async Task LoadCompaniesAsync()
    {
        try
        {
            var companies = await _companyService.GetUserCompaniesAsync(_session.UserId);
            Companies.Clear();
            foreach (var c in companies.OrderBy(c => c.Name))
            {
                Companies.Add(new CompanyListItemViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    TradingName = c.TradingName ?? "",
                    IsCurrent = c.Id == _session.CompanyId
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task Switch(CompanyListItemViewModel? item)
    {
        if (item == null || item.Id == _session.CompanyId)
            return;

        try
        {
            var company = await _companyService.GetCompanyAsync(item.Id)
                ?? throw new InvalidOperationException("Company could not be loaded.");

            _mainViewModel.SetSession(_session.UserId, _session.UserName, company.Id, company.Name);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void CreateNew()
    {
        _mainViewModel.ShowCompanySetupCommand.Execute(null);
    }
}

public sealed class CompanyListItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string TradingName { get; set; } = "";
    public bool IsCurrent { get; set; }
    public bool CanSwitch => !IsCurrent;
    public string DisplayName => IsCurrent ? $"{Name} (current)" : Name;
}
