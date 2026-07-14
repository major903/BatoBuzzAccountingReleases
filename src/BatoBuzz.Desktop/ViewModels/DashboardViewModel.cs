using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BatoBuzz.Application.Interfaces;
using BatoBuzz.Desktop.Services;

namespace BatoBuzz.Desktop.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IDashboardService _dashboardService;
    private readonly DesktopSession _session;

    [ObservableProperty]
    private decimal _cashBalance;

    [ObservableProperty]
    private decimal _bankBalance;

    [ObservableProperty]
    private decimal _totalSalesMonth;

    [ObservableProperty]
    private decimal _totalPurchasesMonth;

    [ObservableProperty]
    private decimal _totalReceivables;

    [ObservableProperty]
    private decimal _totalPayables;

    [ObservableProperty]
    private int _lowStockItemsCount;

    [ObservableProperty]
    private decimal _grossProfitMonth;

    [ObservableProperty]
    private int _outstandingInvoices;

    [ObservableProperty]
    private int _outstandingBills;

    [ObservableProperty]
    private string _statusMessage = "";

    public DashboardViewModel(IDashboardService dashboardService, DesktopSession session)
    {
        _dashboardService = dashboardService;
        _session = session;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (!_session.CompanyId.HasValue)
            return;

        try
        {
            var dashboard = await _dashboardService.GetDashboardAsync(_session.CompanyId.Value);
            CashBalance = dashboard.CashBalance;
            BankBalance = dashboard.BankBalance;
            TotalSalesMonth = dashboard.TotalSalesMonth;
            TotalPurchasesMonth = dashboard.TotalPurchasesMonth;
            TotalReceivables = dashboard.TotalReceivables;
            TotalPayables = dashboard.TotalPayables;
            LowStockItemsCount = dashboard.LowStockItemsCount;
            GrossProfitMonth = dashboard.GrossProfitMonth;
            OutstandingInvoices = dashboard.OutstandingInvoices;
            OutstandingBills = dashboard.OutstandingBills;
            StatusMessage = "";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
