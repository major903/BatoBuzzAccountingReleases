using BatoBuzz.Contracts.Requests;
using BatoBuzz.Contracts.Responses;

namespace BatoBuzz.Application.Interfaces;

public interface IAccountingService
{
    Task<JournalEntryDto> CreateJournalAsync(CreateJournalRequest request, Guid userId);
    Task<JournalEntryDto> PostJournalAsync(Guid journalId, Guid userId);
    Task<JournalEntryDto> ReverseJournalAsync(Guid journalId, string reason, Guid userId);
    Task<JournalEntryDto> ReverseJournalAsync(Guid journalId, CorrectPostedDocumentRequest request, Guid userId);
    Task<IReadOnlyList<JournalEntryDto>> GetJournalsAsync(Guid companyId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<TrialBalanceReportDto> GetTrialBalanceAsync(Guid companyId, DateTime fromDate, DateTime toDate);
    Task<ProfitAndLossReportDto> GetProfitAndLossAsync(Guid companyId, DateTime fromDate, DateTime toDate);
    Task<BalanceSheetReportDto> GetBalanceSheetAsync(Guid companyId, DateTime asOfDate);
    Task<GeneralLedgerReportDto> GetGeneralLedgerAsync(Guid companyId, Guid ledgerId, DateTime fromDate, DateTime toDate);
    Task SetLineClearedAsync(Guid journalLineId, bool cleared, DateTime? clearedDate, Guid userId);
}

public interface ISalesService
{
    Task<SalesInvoiceDto> CreateInvoiceAsync(CreateSalesInvoiceRequest request, Guid userId);
    Task<SalesInvoiceDto> UpdateDraftInvoiceAsync(Guid invoiceId, CreateSalesInvoiceRequest request, Guid userId);
    Task<SalesInvoiceDto> PostInvoiceAsync(Guid invoiceId, Guid userId);
    Task<ReceiptDto> RecordReceiptAsync(CreateReceiptRequest request, Guid userId);
    Task<SalesInvoiceDto> CancelInvoiceAsync(Guid invoiceId, CorrectPostedDocumentRequest request, Guid userId);
    Task<OperationalNoteDto> IssueCreditNoteAsync(Guid invoiceId, CorrectPostedDocumentRequest request, Guid userId);
    Task DeleteDraftInvoiceAsync(Guid invoiceId, Guid userId);
    Task<ReceiptDto> ReverseReceiptAsync(Guid receiptId, CorrectPostedDocumentRequest request, Guid userId);
    Task<IReadOnlyList<SalesInvoiceDto>> GetInvoicesAsync(Guid companyId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<IReadOnlyList<ReceiptDto>> GetReceiptsAsync(
        Guid companyId,
        DateTime? fromDate = null,
        DateTime? toDate = null);
    Task<IReadOnlyList<AgeingItemDto>> GetReceivablesAgeingAsync(Guid companyId, DateTime asOfDate);
}

public interface IPurchaseService
{
    Task<PurchaseBillDto> CreateBillAsync(CreatePurchaseBillRequest request, Guid userId);
    Task<PurchaseBillDto> UpdateDraftBillAsync(Guid billId, CreatePurchaseBillRequest request, Guid userId);
    Task<PurchaseBillDto> PostBillAsync(Guid billId, Guid userId);
    Task<PaymentDto> RecordPaymentAsync(CreatePaymentRequest request, Guid userId);
    Task<PurchaseBillDto> CancelBillAsync(Guid billId, CorrectPostedDocumentRequest request, Guid userId);
    Task<OperationalNoteDto> IssueDebitNoteAsync(Guid billId, CorrectPostedDocumentRequest request, Guid userId);
    Task DeleteDraftBillAsync(Guid billId, Guid userId);
    Task<PaymentDto> ReversePaymentAsync(Guid paymentId, CorrectPostedDocumentRequest request, Guid userId);
    Task<IReadOnlyList<PurchaseBillDto>> GetBillsAsync(Guid companyId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<IReadOnlyList<PaymentDto>> GetPaymentsAsync(
        Guid companyId,
        DateTime? fromDate = null,
        DateTime? toDate = null);
    Task<IReadOnlyList<AgeingItemDto>> GetPayablesAgeingAsync(Guid companyId, DateTime asOfDate);
}

public interface IInventoryService
{
    Task<ItemDto> CreateItemAsync(CreateItemRequest request, Guid userId);
    Task<ItemDto> UpdateItemAsync(Guid itemId, string name, string? code, decimal standardCost, decimal salePrice, decimal? reorderLevel, bool isActive, Guid userId);
    Task RecordStockMovementAsync(CreateStockMovementRequest request, Guid userId);
    Task AdjustStockAsync(CreateStockAdjustmentRequest request, Guid userId);
    Task<IReadOnlyList<StockBalanceDto>> GetStockBalancesAsync(Guid companyId, Guid? warehouseId = null);
    Task<IReadOnlyList<InventoryReportDto>> GetInventoryReportAsync(Guid companyId, Guid? warehouseId = null);
    Task<IReadOnlyList<ItemDto>> GetLowStockItemsAsync(Guid companyId);
    Task<IReadOnlyList<StockMovementDto>> GetStockMovementsAsync(Guid companyId);
    Task ReverseStockMovementAsync(Guid stockMovementId, CorrectPostedDocumentRequest request, Guid userId);
}

public interface ICompanyService
{
    Task<CompanyDto> CreateCompanyAsync(CreateCompanyRequest request, Guid userId);
    Task<CompanyDto> UpdateCompanyAsync(Guid companyId, CreateCompanyRequest request, Guid userId);
    Task<CompanyDto?> GetCompanyAsync(Guid companyId);
    Task<IReadOnlyList<CompanyDto>> GetUserCompaniesAsync(Guid userId);
    Task<FinancialYearDto?> GetCurrentFinancialYearAsync(Guid companyId);
    Task<CompanyDto> SetPeriodLockDateAsync(Guid companyId, DateTime? lockDate, Guid userId);
}

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task<AuthResult> LoginOfflineAsync(LoginRequest request);
    Task LogoutAsync(Guid userId);
    Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
}

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(Guid companyId);
}

public interface ITdsService
{
    Task<TdsRateDto> CreateRateAsync(CreateTdsRateRequest request, Guid userId);
    Task<TdsRateDto> UpdateRateAsync(Guid rateId, string name, decimal ratePercent, string? description, bool isActive);
    Task<IReadOnlyList<TdsRateDto>> GetRatesAsync(Guid companyId, bool activeOnly = true);
}
