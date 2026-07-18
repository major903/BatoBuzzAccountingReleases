using BatoBuzz.Domain.Entities;

namespace BatoBuzz.Application.Interfaces;

public interface ICompanyRepository : IRepository<Company>
{
    Task<Company?> GetByIdWithDetailsAsync(Guid id);
    Task<FinancialYear?> GetCurrentFinancialYearAsync(Guid companyId);
    Task AddFinancialYearAsync(FinancialYear financialYear);
    Task<bool> ExistsByNameAsync(Guid createdByUserId, string name);
}

public interface IAccountGroupRepository : IRepository<AccountGroup>
{
    Task<IReadOnlyList<AccountGroup>> GetByCompanyAsync(Guid companyId);
}

public interface ILedgerRepository : IRepository<Ledger>
{
    Task<Ledger?> GetByIdWithAccountGroupAsync(Guid id);
    Task<IReadOnlyList<Ledger>> GetByCompanyAsync(Guid companyId);
    Task<IReadOnlyList<Ledger>> GetByAccountGroupAsync(Guid accountGroupId);
    Task<IReadOnlyList<Ledger>> GetByTypeAsync(Guid companyId, int ledgerType);
}

public interface IJournalEntryRepository : IRepository<JournalEntry>
{
    Task<JournalEntry?> GetByIdWithLinesAsync(Guid id);
    Task<IReadOnlyList<JournalEntry>> GetByCompanyAsync(Guid companyId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<IReadOnlyList<JournalEntry>> GetByLedgerAsync(Guid ledgerId, DateTime fromDate, DateTime toDate);
    Task<string> GetNextEntryNumberAsync(Guid companyId, int voucherType);
    Task<JournalLine?> GetLineByIdAsync(Guid lineId);
}

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByIdWithLedgerAsync(Guid id);
    Task<IReadOnlyList<Customer>> GetByCompanyAsync(Guid companyId, bool activeOnly = true);
    Task<bool> ExistsByNameAsync(Guid companyId, string name);
}

public interface ISupplierRepository : IRepository<Supplier>
{
    Task<Supplier?> GetByIdWithLedgerAsync(Guid id);
    Task<IReadOnlyList<Supplier>> GetByCompanyAsync(Guid companyId, bool activeOnly = true);
    Task<bool> ExistsByNameAsync(Guid companyId, string name);
}

public interface ISalesInvoiceRepository : IRepository<SalesInvoice>
{
    Task<SalesInvoice?> GetByIdWithDetailsAsync(Guid id);
    Task AddLineAsync(SalesInvoiceLine line);
    Task<IReadOnlyList<SalesInvoice>> GetByCompanyAsync(Guid companyId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<IReadOnlyList<SalesInvoice>> GetByCustomerAsync(Guid customerId);
    Task<string> GetNextInvoiceNumberAsync(Guid companyId);
    Task<string> GetNextCreditNoteNumberAsync(Guid companyId);
}

public interface IReceiptRepository : IRepository<Receipt>
{
    Task<Receipt?> GetByIdWithDetailsAsync(Guid id);
    Task<IReadOnlyList<Receipt>> GetByCustomerAsync(Guid customerId);
    Task<IReadOnlyList<Receipt>> GetByCompanyAsync(
        Guid companyId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<string> GetNextReceiptNumberAsync(Guid companyId);
}

public interface IPurchaseBillRepository : IRepository<PurchaseBill>
{
    Task<PurchaseBill?> GetByIdWithDetailsAsync(Guid id);
    Task AddLineAsync(PurchaseBillLine line);
    Task<IReadOnlyList<PurchaseBill>> GetByCompanyAsync(Guid companyId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<IReadOnlyList<PurchaseBill>> GetBySupplierAsync(Guid supplierId);
    Task<string> GetNextBillNumberAsync(Guid companyId);
    Task<string> GetNextDebitNoteNumberAsync(Guid companyId);
}

public interface IPaymentRepository : IRepository<Payment>
{
    Task<Payment?> GetByIdWithDetailsAsync(Guid id);
    Task<IReadOnlyList<Payment>> GetBySupplierAsync(Guid supplierId);
    Task<IReadOnlyList<Payment>> GetByCompanyAsync(
        Guid companyId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<string> GetNextPaymentNumberAsync(Guid companyId);
}

public interface IItemRepository : IRepository<Item>
{
    Task<Item?> GetByIdWithDetailsAsync(Guid id);
    Task<IReadOnlyList<Item>> GetByCompanyAsync(Guid companyId, bool activeOnly = true);
    Task<IReadOnlyList<Item>> GetLowStockItemsAsync(Guid companyId);
}

public interface IWarehouseRepository : IRepository<Warehouse>
{
    Task<IReadOnlyList<Warehouse>> GetByCompanyAsync(Guid companyId);
}

public interface IUnitRepository : IRepository<Unit>
{
    Task<IReadOnlyList<Unit>> GetByCompanyAsync(Guid companyId);
}

public interface IItemCategoryRepository : IRepository<ItemCategory>
{
    Task<IReadOnlyList<ItemCategory>> GetByCompanyAsync(Guid companyId);
}

public interface IStockMovementRepository : IRepository<StockMovement>
{
    Task<IReadOnlyList<StockMovement>> GetByItemAsync(Guid itemId, Guid? warehouseId = null);
    Task<IReadOnlyList<StockMovement>> GetByWarehouseAsync(Guid warehouseId);
    Task<IReadOnlyList<StockMovement>> GetBySourceDocumentAsync(
        Guid companyId, Guid documentId, string documentType);
    Task<IReadOnlyList<StockMovement>> GetItemWarehouseChronologyAsync(
        Guid itemId, Guid warehouseId);
}

public interface IStockBalanceRepository : IRepository<StockBalance>
{
    Task<StockBalance?> GetByItemWarehouseAsync(Guid itemId, Guid warehouseId);
    Task<IReadOnlyList<StockBalance>> GetByCompanyAsync(Guid companyId, Guid? warehouseId = null);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUserNameAsync(string userName);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdWithRolesAsync(Guid id);
    Task<bool> ExistsByUserNameAsync(string userName);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog);
    Task<IReadOnlyList<AuditLog>> GetByCompanyAsync(Guid companyId, DateTime? fromDate = null, DateTime? toDate = null, int pageSize = 50);
}

public interface ITdsRateRepository : IRepository<TdsRate>
{
    Task<IReadOnlyList<TdsRate>> GetByCompanyAsync(Guid companyId, bool activeOnly = true);
}
