using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Responses;
using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DashboardDto> GetDashboardAsync(Guid companyId)
    {
        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var now = DateTime.Now;

        // Cash and Bank balances
        var ledgers = await _unitOfWork.Ledgers.GetByCompanyAsync(companyId);
        var cashBalance = ledgers.Where(l => l.LedgerType == LedgerType.Cash).Sum(l => l.CurrentBalance);
        var bankBalance = ledgers.Where(l => l.LedgerType == LedgerType.Bank).Sum(l => l.CurrentBalance);

        // Monthly sales
        var invoices = await _unitOfWork.SalesInvoices.GetByCompanyAsync(companyId, monthStart, now);
        var totalSalesMonth = invoices.Where(i => i.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid
            or InvoiceStatus.Paid or InvoiceStatus.Overdue).Sum(i => i.TotalAmount);
        var allInvoices = await _unitOfWork.SalesInvoices.GetByCompanyAsync(companyId);
        var outstandingInvoices = allInvoices.Count(i => i.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid or InvoiceStatus.Overdue);

        // Monthly purchases
        var bills = await _unitOfWork.PurchaseBills.GetByCompanyAsync(companyId, monthStart, now);
        var totalPurchasesMonth = bills.Where(b => b.Status is BillStatus.Received or BillStatus.PartiallyPaid
            or BillStatus.Paid or BillStatus.Overdue).Sum(b => b.TotalAmount);
        var allBills = await _unitOfWork.PurchaseBills.GetByCompanyAsync(companyId);
        var outstandingBills = allBills.Count(b => b.Status is BillStatus.Received or BillStatus.PartiallyPaid or BillStatus.Overdue);

        // Receivables and Payables
        var customers = await _unitOfWork.Customers.GetByCompanyAsync(companyId);
        var totalReceivables = customers.Sum(c => Math.Max(0, c.CurrentBalance));

        var suppliers = await _unitOfWork.Suppliers.GetByCompanyAsync(companyId);
        var totalPayables = suppliers.Sum(s => Math.Max(0, s.CurrentBalance));

        // Low stock
        var lowStockItems = await _unitOfWork.Items.GetLowStockItemsAsync(companyId);

        // Gross profit
        var grossProfitMonth = (await new AccountingService(_unitOfWork).GetProfitAndLossAsync(companyId, monthStart, now)).GrossProfit;

        return new DashboardDto
        {
            CashBalance = cashBalance,
            BankBalance = bankBalance,
            TotalSalesMonth = totalSalesMonth,
            TotalPurchasesMonth = totalPurchasesMonth,
            TotalReceivables = totalReceivables,
            TotalPayables = totalPayables,
            LowStockItemsCount = lowStockItems.Count,
            GrossProfitMonth = grossProfitMonth,
            OutstandingInvoices = outstandingInvoices,
            OutstandingBills = outstandingBills
        };
    }
}
