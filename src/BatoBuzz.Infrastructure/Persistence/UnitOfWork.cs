using BatoBuzz.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace BatoBuzz.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly BatoBuzzDbContext _context;
    private IDbContextTransaction? _currentTransaction;

    public ICompanyRepository Companies { get; }
    public ILedgerRepository Ledgers { get; }
    public IJournalEntryRepository JournalEntries { get; }
    public ICustomerRepository Customers { get; }
    public ISupplierRepository Suppliers { get; }
    public ISalesInvoiceRepository SalesInvoices { get; }
    public IReceiptRepository Receipts { get; }
    public IPurchaseBillRepository PurchaseBills { get; }
    public IPaymentRepository Payments { get; }
    public IItemRepository Items { get; }
    public IStockBalanceRepository StockBalances { get; }
    public IUnitRepository Units { get; }
    public IWarehouseRepository Warehouses { get; }
    public IStockMovementRepository StockMovements { get; }
    public IUserRepository Users { get; }
    public IAuditLogRepository AuditLogs { get; }
    public ITdsRateRepository TdsRates { get; }

    public UnitOfWork(BatoBuzzDbContext context)
    {
        _context = context;
        Companies = new CompanyRepository(context);
        Ledgers = new LedgerRepository(context);
        JournalEntries = new JournalEntryRepository(context);
        Customers = new CustomerRepository(context);
        Suppliers = new SupplierRepository(context);
        SalesInvoices = new SalesInvoiceRepository(context);
        Receipts = new ReceiptRepository(context);
        PurchaseBills = new PurchaseBillRepository(context);
        Payments = new PaymentRepository(context);
        Items = new ItemRepository(context);
        StockBalances = new StockBalanceRepository(context);
        Units = new UnitRepository(context);
        Warehouses = new WarehouseRepository(context);
        StockMovements = new StockMovementRepository(context);
        Users = new UserRepository(context);
        AuditLogs = new AuditLogRepository(context);
        TdsRates = new TdsRateRepository(context);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync()
    {
        _currentTransaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.CommitAsync();
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        var transaction = _currentTransaction;
        if (transaction == null)
        {
            _context.ChangeTracker.Clear();
            return;
        }

        try
        {
            await transaction.RollbackAsync();
        }
        finally
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                _currentTransaction = null;
                _context.ChangeTracker.Clear();
            }
        }
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
