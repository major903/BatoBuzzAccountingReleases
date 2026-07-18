using BatoBuzz.Contracts.Common;

namespace BatoBuzz.Contracts.Responses;

public class TrialBalanceReportDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<TrialBalanceItemDto> Items { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
}

public class TrialBalanceItemDto
{
    public Guid LedgerId { get; set; }
    public string LedgerName { get; set; } = null!;
    public AccountTypeDto AccountType { get; set; }
    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal PeriodDebit { get; set; }
    public decimal PeriodCredit { get; set; }
    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }
}

public class ProfitAndLossReportDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalCostOfSales { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public List<PLAccountDto> IncomeAccounts { get; set; } = new();
    public List<PLAccountDto> CostOfSalesAccounts { get; set; } = new();
    public List<PLAccountDto> ExpenseAccounts { get; set; } = new();
}

public class PLAccountDto
{
    public Guid LedgerId { get; set; }
    public string LedgerName { get; set; } = null!;
    public decimal Amount { get; set; }
}

public class BalanceSheetReportDto
{
    public Guid CompanyId { get; set; }
    public DateTime AsOfDate { get; set; }
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal TotalEquity { get; set; }
    public decimal NetAssets { get; set; }
    public List<BSAccountDto> Assets { get; set; } = new();
    public List<BSAccountDto> Liabilities { get; set; } = new();
    public List<BSAccountDto> Equity { get; set; } = new();
}

public class BSAccountDto
{
    public Guid LedgerId { get; set; }
    public string LedgerName { get; set; } = null!;
    public decimal Amount { get; set; }
}

public class GeneralLedgerReportDto
{
    public Guid CompanyId { get; set; }
    public Guid LedgerId { get; set; }
    public string LedgerName { get; set; } = null!;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public List<GLTransactionDto> Transactions { get; set; } = new();
    public decimal ClosingBalance { get; set; }
}

public class GLTransactionDto
{
    public Guid LineId { get; set; }
    public DateTime Date { get; set; }
    public string VoucherType { get; set; } = null!;
    public string EntryNumber { get; set; } = null!;
    public string? Narration { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
    public bool IsCleared { get; set; }
    public DateTime? ClearedDate { get; set; }
}

public class AgeingItemDto
{
    public Guid ContactId { get; set; }
    public string ContactName { get; set; } = null!;
    public decimal CurrentAmount { get; set; }
    public decimal Days1To30 { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Days61To90 { get; set; }
    public decimal Over90Days { get; set; }
    public decimal TotalAmount { get; set; }
}

public class InventoryReportDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public string? CategoryName { get; set; }
    public string UnitName { get; set; } = null!;
    public string? WarehouseName { get; set; }
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal TotalValue { get; set; }
    public decimal? ReorderLevel { get; set; }
    public bool IsLowStock { get; set; }
}

public class StockMovementDto
{
    public Guid Id { get; set; }
    public string ItemName { get; set; } = null!;
    public string WarehouseName { get; set; } = null!;
    public int MovementType { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime MovementDate { get; set; }
    public string? Narration { get; set; }
    public bool IsReversed { get; set; }
    public bool CanReverse { get; set; }
}
