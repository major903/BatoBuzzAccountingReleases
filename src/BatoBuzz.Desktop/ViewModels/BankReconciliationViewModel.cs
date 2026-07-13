using BatoBuzz.Application.Interfaces;
using BatoBuzz.Desktop.Services;
using BatoBuzz.Domain.Entities;
using BatoBuzz.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BatoBuzz.Desktop.ViewModels;

public partial class BankReconciliationViewModel : ObservableObject
{
    private readonly IAccountingService _accountingService;
    private readonly DesktopDataService _dataService;
    private readonly DesktopSession _session;

    [ObservableProperty]
    private ObservableCollection<Ledger> _bankLedgers = new();

    [ObservableProperty]
    private Ledger? _selectedLedger;

    [ObservableProperty]
    private DateTime _fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

    [ObservableProperty]
    private DateTime _toDate = DateTime.Now;

    [ObservableProperty]
    private ObservableCollection<ReconciliationLineViewModel> _transactions = new();

    [ObservableProperty]
    private decimal _openingBalance;

    [ObservableProperty]
    private decimal _closingBalance;

    [ObservableProperty]
    private decimal _reconciledBalance;

    [ObservableProperty]
    private decimal _difference;

    [ObservableProperty]
    private string _statusMessage = "";

    public BankReconciliationViewModel(IAccountingService accountingService, DesktopDataService dataService, DesktopSession session)
    {
        _accountingService = accountingService;
        _dataService = dataService;
        _session = session;
        _ = LoadBankLedgersAsync();
    }

    private async Task LoadBankLedgersAsync()
    {
        if (!_session.CompanyId.HasValue)
            return;

        var ledgers = await _dataService.GetLedgersAsync(_session.CompanyId.Value);
        BankLedgers = new ObservableCollection<Ledger>(
            ledgers.Where(l => l.LedgerType == LedgerType.Bank).OrderBy(l => l.Name));
        SelectedLedger ??= BankLedgers.FirstOrDefault();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        if (!_session.CompanyId.HasValue || SelectedLedger == null)
        {
            StatusMessage = "Select a bank ledger first.";
            return;
        }

        try
        {
            var report = await _accountingService.GetGeneralLedgerAsync(
                _session.CompanyId.Value, SelectedLedger.Id, FromDate, ToDate);

            OpeningBalance = report.OpeningBalance;
            ClosingBalance = report.ClosingBalance;

            Transactions = new ObservableCollection<ReconciliationLineViewModel>(
                report.Transactions.Select(t => new ReconciliationLineViewModel(OnLineToggled, t.IsCleared)
                {
                    LineId = t.LineId,
                    Date = t.Date,
                    VoucherType = t.VoucherType,
                    EntryNumber = t.EntryNumber,
                    Narration = t.Narration ?? "",
                    Debit = t.Debit,
                    Credit = t.Credit,
                    Balance = t.Balance
                }));

            RecalculateSummary();
            StatusMessage = "";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task OnLineToggled(Guid lineId, bool cleared)
    {
        try
        {
            await _accountingService.SetLineClearedAsync(lineId, cleared, cleared ? DateTime.Now : null, _session.UserId);
            RecalculateSummary();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void RecalculateSummary()
    {
        ReconciledBalance = OpeningBalance + Transactions.Where(t => t.IsCleared).Sum(t => t.Debit - t.Credit);
        Difference = ClosingBalance - ReconciledBalance;
    }
}

public partial class ReconciliationLineViewModel : ObservableObject
{
    private readonly Func<Guid, bool, Task> _onToggle;
    private bool _initializing = true;

    public ReconciliationLineViewModel(Func<Guid, bool, Task> onToggle, bool initialCleared)
    {
        _onToggle = onToggle;
        IsCleared = initialCleared; // guarded by _initializing so this doesn't re-persist on load
        _initializing = false;
    }

    public Guid LineId { get; set; }
    public DateTime Date { get; set; }
    public string VoucherType { get; set; } = "";
    public string EntryNumber { get; set; } = "";
    public string Narration { get; set; } = "";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }

    [ObservableProperty]
    private bool _isCleared;

    partial void OnIsClearedChanged(bool value)
    {
        if (_initializing)
            return;
        _ = _onToggle(LineId, value);
    }
}
