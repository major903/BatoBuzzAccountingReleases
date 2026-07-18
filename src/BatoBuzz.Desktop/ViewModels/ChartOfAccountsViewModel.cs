using BatoBuzz.Desktop.Services;
using BatoBuzz.Domain.Entities;
using BatoBuzz.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BatoBuzz.Desktop.ViewModels;

public partial class ChartOfAccountsViewModel : ObservableObject
{
    private readonly DesktopDataService _dataService;
    private readonly DesktopSession _session;

    [ObservableProperty]
    private ObservableCollection<AccountGroup> _accountGroups = new();

    [ObservableProperty]
    private ObservableCollection<Ledger> _ledgers = new();

    [ObservableProperty]
    private AccountGroup? _selectedAccountGroup;

    [ObservableProperty]
    private Ledger? _selectedLedger;

    [ObservableProperty]
    private string _accountGroupName = "";

    [ObservableProperty]
    private string _accountType = nameof(BatoBuzz.Domain.Enums.AccountType.Expense);

    [ObservableProperty]
    private string _ledgerName = "";

    [ObservableProperty]
    private string _ledgerCode = "";

    [ObservableProperty]
    private string _ledgerType = nameof(BatoBuzz.Domain.Enums.LedgerType.General);

    [ObservableProperty]
    private string _statusMessage = "";

    public string[] AccountTypes { get; } = Enum.GetNames<AccountType>();
    public string[] LedgerTypes { get; } =
    [
        nameof(BatoBuzz.Domain.Enums.LedgerType.General),
        nameof(BatoBuzz.Domain.Enums.LedgerType.Bank),
        nameof(BatoBuzz.Domain.Enums.LedgerType.Cash),
        nameof(BatoBuzz.Domain.Enums.LedgerType.Tax),
        nameof(BatoBuzz.Domain.Enums.LedgerType.Employee)
    ];

    public ChartOfAccountsViewModel(DesktopDataService dataService, DesktopSession session)
    {
        _dataService = dataService;
        _session = session;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    [RelayCommand]
    private async Task SaveAccountGroup()
    {
        if (!_session.CompanyId.HasValue)
        {
            StatusMessage = "No company selected.";
            return;
        }

        try
        {
            if (!Enum.TryParse<AccountType>(AccountType, out var accountType))
                throw new InvalidOperationException("Select a valid account type.");

            var group = await _dataService.CreateAccountGroupAsync(
                _session.CompanyId.Value, AccountGroupName, accountType, _session.UserId);
            AccountGroupName = "";
            await LoadAsync();
            SelectedAccountGroup = AccountGroups.FirstOrDefault(candidate => candidate.Id == group.Id);
            StatusMessage = $"Created account group {group.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task RenameSelectedAccountGroup()
    {
        if (SelectedAccountGroup == null)
        {
            StatusMessage = "Select an account group to rename.";
            return;
        }

        try
        {
            var group = await _dataService.RenameAccountGroupAsync(
                _session.CompanyId ?? throw new InvalidOperationException("No company selected."),
                SelectedAccountGroup.Id, AccountGroupName, _session.UserId);
            await LoadAsync();
            SelectedAccountGroup = AccountGroups.FirstOrDefault(candidate => candidate.Id == group.Id);
            StatusMessage = $"Renamed account group to {group.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ToggleSelectedAccountGroupActive()
    {
        if (SelectedAccountGroup == null)
        {
            StatusMessage = "Select an account group to activate or deactivate.";
            return;
        }

        try
        {
            await _dataService.SetAccountGroupActiveAsync(
                _session.CompanyId ?? throw new InvalidOperationException("No company selected."),
                SelectedAccountGroup.Id, !SelectedAccountGroup.IsActive, _session.UserId);
            var groupName = SelectedAccountGroup.Name;
            var active = !SelectedAccountGroup.IsActive;
            await LoadAsync();
            StatusMessage = active ? $"Activated account group {groupName}." : $"Deactivated account group {groupName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    partial void OnSelectedAccountGroupChanged(AccountGroup? value)
    {
        if (value != null)
            AccountGroupName = value.Name;
    }

    [RelayCommand]
    private async Task SaveLedger()
    {
        if (!_session.CompanyId.HasValue)
        {
            StatusMessage = "No company selected.";
            return;
        }

        try
        {
            if (SelectedAccountGroup == null)
                throw new InvalidOperationException("Select an account group for the ledger.");
            if (!Enum.TryParse<LedgerType>(LedgerType, out var ledgerType))
                throw new InvalidOperationException("Select a valid ledger type.");

            var ledger = await _dataService.CreateLedgerAsync(
                _session.CompanyId.Value,
                SelectedAccountGroup.Id,
                LedgerName,
                string.IsNullOrWhiteSpace(LedgerCode) ? null : LedgerCode,
                ledgerType,
                _session.UserId);
            LedgerName = "";
            LedgerCode = "";
            await LoadAsync();
            StatusMessage = $"Created ledger {ledger.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task UpdateSelectedLedger()
    {
        if (SelectedLedger == null)
        {
            StatusMessage = "Select a ledger to edit.";
            return;
        }

        try
        {
            var ledger = await _dataService.UpdateLedgerAsync(
                SelectedLedger.Id, LedgerName, LedgerCode, SelectedLedger.IsActive, _session.UserId);
            await LoadAsync();
            SelectedLedger = Ledgers.FirstOrDefault(candidate => candidate.Id == ledger.Id);
            StatusMessage = $"Updated ledger {ledger.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ToggleSelectedLedgerActive()
    {
        if (SelectedLedger == null)
        {
            StatusMessage = "Select a ledger to activate or deactivate.";
            return;
        }

        try
        {
            var ledger = await _dataService.UpdateLedgerAsync(
                SelectedLedger.Id, SelectedLedger.Name, SelectedLedger.Code, !SelectedLedger.IsActive, _session.UserId);
            await LoadAsync();
            SelectedLedger = Ledgers.FirstOrDefault(candidate => candidate.Id == ledger.Id);
            StatusMessage = ledger.IsActive ? $"Activated ledger {ledger.Name}." : $"Deactivated ledger {ledger.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    partial void OnSelectedLedgerChanged(Ledger? value)
    {
        if (value == null)
            return;
        LedgerName = value.Name;
        LedgerCode = value.Code ?? "";
        LedgerType = value.LedgerType.ToString();
        SelectedAccountGroup = AccountGroups.FirstOrDefault(group => group.Id == value.AccountGroupId);
    }

    private async Task LoadAsync()
    {
        if (!_session.CompanyId.HasValue)
            return;

        try
        {
            var selectedId = SelectedAccountGroup?.Id;
            var groups = await _dataService.GetAccountGroupsAsync(_session.CompanyId.Value);
            var ledgers = await _dataService.GetLedgersAsync(_session.CompanyId.Value);
            AccountGroups = new ObservableCollection<AccountGroup>(groups);
            Ledgers = new ObservableCollection<Ledger>(ledgers.OrderBy(ledger => ledger.AccountGroup.AccountType).ThenBy(ledger => ledger.AccountGroup.Name).ThenBy(ledger => ledger.Name));
            SelectedAccountGroup = AccountGroups.FirstOrDefault(group => group.Id == selectedId) ?? AccountGroups.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
