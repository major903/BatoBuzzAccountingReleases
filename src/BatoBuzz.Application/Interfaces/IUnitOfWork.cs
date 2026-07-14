namespace BatoBuzz.Application.Interfaces;

/// <summary>
/// Unit of Work pattern for transaction management.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    ICompanyRepository Companies { get; }
    ILedgerRepository Ledgers { get; }
    IJournalEntryRepository JournalEntries { get; }
    ICustomerRepository Customers { get; }
    ISupplierRepository Suppliers { get; }
    ISalesInvoiceRepository SalesInvoices { get; }
    IReceiptRepository Receipts { get; }
    IPurchaseBillRepository PurchaseBills { get; }
    IPaymentRepository Payments { get; }
    IItemRepository Items { get; }
    IStockBalanceRepository StockBalances { get; }
    IUnitRepository Units { get; }
    IWarehouseRepository Warehouses { get; }
    IStockMovementRepository StockMovements { get; }
    IUserRepository Users { get; }
    IAuditLogRepository AuditLogs { get; }
    ITdsRateRepository TdsRates { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
