namespace BatoBuzz.Contracts.Responses;

/// <summary>
/// Dashboard KPI data.
/// </summary>
public class DashboardDto
{
    public decimal CashBalance { get; set; }
    public decimal BankBalance { get; set; }
    public decimal TotalSalesMonth { get; set; }
    public decimal TotalPurchasesMonth { get; set; }
    public decimal TotalReceivables { get; set; }
    public decimal TotalPayables { get; set; }
    public int LowStockItemsCount { get; set; }
    public decimal GrossProfitMonth { get; set; }
    public int OutstandingInvoices { get; set; }
    public int OutstandingBills { get; set; }
}
