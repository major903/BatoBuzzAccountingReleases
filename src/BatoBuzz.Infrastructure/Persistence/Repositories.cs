using BatoBuzz.Application.Interfaces;
using BatoBuzz.Domain.Entities;
using BatoBuzz.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;

namespace BatoBuzz.Infrastructure.Persistence;

public abstract class Repository<T> : IRepository<T> where T : class
{
    protected readonly BatoBuzzDbContext _context;
    protected readonly DbSet<T> _dbSet;

    protected Repository(BatoBuzzDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id) => await _dbSet.FindAsync(id);
    public virtual async Task<IReadOnlyList<T>> GetAllAsync() => await _dbSet.ToListAsync();
    public virtual async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate) =>
        await _dbSet.Where(predicate).ToListAsync();
    public virtual async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        return entity;
    }
    public virtual Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }
    public virtual Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }
    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null) =>
        predicate == null ? await _dbSet.CountAsync() : await _dbSet.CountAsync(predicate);

    protected async Task<long> AllocateNextDocumentNumberAsync(Guid companyId, string sequenceKey)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        IDbContextTransaction? transaction = null;
        var ownsTransaction = false;

        try
        {
            if (!wasOpen)
                await connection.OpenAsync();

            transaction = _context.Database.CurrentTransaction;
            ownsTransaction = transaction == null;
            if (ownsTransaction)
                transaction = await _context.Database.BeginTransactionAsync();
            var activeTransaction = transaction
                ?? throw new InvalidOperationException("A document-number transaction could not be started.");

            await using var command = connection.CreateCommand();
            command.Transaction = activeTransaction.GetDbTransaction();
            command.CommandText = """
                INSERT INTO "DocumentSequences" ("CompanyId", "SequenceKey", "LastValue")
                VALUES (@companyId, @sequenceKey, 1)
                ON CONFLICT ("CompanyId", "SequenceKey")
                DO UPDATE SET "LastValue" = "LastValue" + 1
                RETURNING "LastValue";
                """;

            var companyParameter = command.CreateParameter();
            companyParameter.ParameterName = "@companyId";
            companyParameter.Value = companyId;
            command.Parameters.Add(companyParameter);
            var keyParameter = command.CreateParameter();
            keyParameter.ParameterName = "@sequenceKey";
            keyParameter.Value = sequenceKey;
            command.Parameters.Add(keyParameter);

            var nextValue = Convert.ToInt64(await command.ExecuteScalarAsync(),
                System.Globalization.CultureInfo.InvariantCulture);

            if (ownsTransaction)
                await activeTransaction.CommitAsync();

            return nextValue;
        }
        finally
        {
            if (transaction != null && ownsTransaction)
                await transaction.DisposeAsync();
            if (!wasOpen)
                await connection.CloseAsync();
        }
    }
}

public class CompanyRepository : Repository<Company>, ICompanyRepository
{
    public CompanyRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<Company?> GetByIdWithDetailsAsync(Guid id) =>
        await _dbSet.Include(c => c.Branches)
                    .Include(c => c.FinancialYears)
                    .Include(c => c.AccountGroups)
                    .Include(c => c.Ledgers)
                    .Include(c => c.ItemCategories)
                    .Include(c => c.Units)
                    .Include(c => c.Warehouses)
                    .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<FinancialYear?> GetCurrentFinancialYearAsync(Guid companyId) =>
        await _context.FinancialYears.FirstOrDefaultAsync(fy => fy.CompanyId == companyId && fy.IsCurrent);

    public async Task AddFinancialYearAsync(FinancialYear financialYear) =>
        await _context.FinancialYears.AddAsync(financialYear);

    public async Task<bool> ExistsByNameAsync(Guid createdByUserId, string name)
    {
        var normalized = name.Trim().ToUpperInvariant();
        return await _dbSet.AnyAsync(c =>
            c.CreatedByUserId == createdByUserId && c.Name.ToUpper() == normalized);
    }
}

public class AccountGroupRepository : Repository<AccountGroup>, IAccountGroupRepository
{
    public AccountGroupRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<IReadOnlyList<AccountGroup>> GetByCompanyAsync(Guid companyId) =>
        await _dbSet.Where(group => group.CompanyId == companyId).ToListAsync();
}

public class LedgerRepository : Repository<Ledger>, ILedgerRepository
{
    public LedgerRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<Ledger?> GetByIdWithAccountGroupAsync(Guid id) =>
        await _dbSet.Include(l => l.AccountGroup).FirstOrDefaultAsync(l => l.Id == id);

    public async Task<IReadOnlyList<Ledger>> GetByCompanyAsync(Guid companyId) =>
        await _dbSet.Include(l => l.AccountGroup).Where(l => l.CompanyId == companyId).ToListAsync();

    public async Task<IReadOnlyList<Ledger>> GetByAccountGroupAsync(Guid accountGroupId) =>
        await _dbSet.Where(l => l.AccountGroupId == accountGroupId).ToListAsync();

    public async Task<IReadOnlyList<Ledger>> GetByTypeAsync(Guid companyId, int ledgerType) =>
        await _dbSet.Where(l => l.CompanyId == companyId && (int)l.LedgerType == ledgerType).ToListAsync();
}

public class JournalEntryRepository : Repository<JournalEntry>, IJournalEntryRepository
{
    public JournalEntryRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<JournalEntry?> GetByIdWithLinesAsync(Guid id) =>
        await _dbSet.Include(j => j.Lines)
                    .ThenInclude(l => l.Ledger)
                    .ThenInclude(l => l.AccountGroup)
                    .FirstOrDefaultAsync(j => j.Id == id);

    public async Task<IReadOnlyList<JournalEntry>> GetByCompanyAsync(Guid companyId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _dbSet.Where(j => j.CompanyId == companyId);
        if (fromDate.HasValue) query = query.Where(j => j.EntryDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(j => j.EntryDate <= toDate.Value);
        return await query.Include(j => j.Lines).OrderByDescending(j => j.EntryDate).ToListAsync();
    }

    public async Task<IReadOnlyList<JournalEntry>> GetByLedgerAsync(Guid ledgerId, DateTime fromDate, DateTime toDate) =>
        await _dbSet.Include(j => j.Lines)
                    .ThenInclude(l => l.Ledger)
                    .ThenInclude(l => l.AccountGroup)
                    .Where(j => (j.Status == TransactionStatus.Posted ||
                                 (j.Status == TransactionStatus.Reversed && j.ReversedJournalEntryId.HasValue)) &&
                                j.EntryDate >= fromDate && j.EntryDate <= toDate &&
                                j.Lines.Any(l => l.LedgerId == ledgerId))
                    .OrderBy(j => j.EntryDate)
                    .ToListAsync();

    public async Task<string> GetNextEntryNumberAsync(Guid companyId, int voucherType)
    {
        var prefix = voucherType switch
        {
            1 => "SI",
            2 => "PI",
            3 => "RV",
            4 => "PV",
            5 => "CV",
            6 => "JV",
            7 => "DN",
            8 => "CN",
            9 => "SR",
            10 => "PR",
            11 => "OB",
            12 => "SJ",
            99 => "REV",
            _ => "JV"
        };

        var nextNumber = await AllocateNextDocumentNumberAsync(
            companyId, $"Journal:{voucherType}");
        return $"{prefix}-{nextNumber:D6}";
    }

    public async Task<JournalLine?> GetLineByIdAsync(Guid lineId) =>
        await _context.JournalLines.Include(l => l.JournalEntry)
                       .FirstOrDefaultAsync(l => l.Id == lineId);
}

public class CustomerRepository : Repository<Customer>, ICustomerRepository
{
    public CustomerRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<Customer?> GetByIdWithLedgerAsync(Guid id) =>
        await _dbSet.Include(c => c.Ledger).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<IReadOnlyList<Customer>> GetByCompanyAsync(Guid companyId, bool activeOnly = true) =>
        activeOnly
            ? await _dbSet.Where(c => c.CompanyId == companyId && c.IsActive).ToListAsync()
            : await _dbSet.Where(c => c.CompanyId == companyId).ToListAsync();

    public async Task<bool> ExistsByNameAsync(Guid companyId, string name) =>
        await _dbSet.AnyAsync(c => c.CompanyId == companyId && c.Name == name);
}

public class SupplierRepository : Repository<Supplier>, ISupplierRepository
{
    public SupplierRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<Supplier?> GetByIdWithLedgerAsync(Guid id) =>
        await _dbSet.Include(s => s.Ledger).FirstOrDefaultAsync(s => s.Id == id);

    public async Task<IReadOnlyList<Supplier>> GetByCompanyAsync(Guid companyId, bool activeOnly = true) =>
        activeOnly
            ? await _dbSet.Where(s => s.CompanyId == companyId && s.IsActive).ToListAsync()
            : await _dbSet.Where(s => s.CompanyId == companyId).ToListAsync();

    public async Task<bool> ExistsByNameAsync(Guid companyId, string name) =>
        await _dbSet.AnyAsync(s => s.CompanyId == companyId && s.Name == name);
}

public class SalesInvoiceRepository : Repository<SalesInvoice>, ISalesInvoiceRepository
{
    public SalesInvoiceRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<SalesInvoice?> GetByIdWithDetailsAsync(Guid id) =>
        await _dbSet.Include(i => i.Customer)
                    .Include(i => i.Lines)
                    .Include(i => i.PostedJournalEntry)
                    .ThenInclude(j => j!.Lines)
                     .FirstOrDefaultAsync(i => i.Id == id);

    public async Task AddLineAsync(SalesInvoiceLine line) =>
        await _context.SalesInvoiceLines.AddAsync(line);

    public async Task<IReadOnlyList<SalesInvoice>> GetByCompanyAsync(Guid companyId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _dbSet.Include(i => i.Customer)
                          .Include(i => i.Lines)
                          .Include(i => i.PostedJournalEntry)
                          .ThenInclude(j => j!.ReversalJournalEntry)
                          .Where(i => i.CompanyId == companyId);
        if (fromDate.HasValue) query = query.Where(i => i.InvoiceDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(i => i.InvoiceDate <= toDate.Value);
        return await query.OrderByDescending(i => i.InvoiceDate).ToListAsync();
    }

    public async Task<IReadOnlyList<SalesInvoice>> GetByCustomerAsync(Guid customerId) =>
        await _dbSet.Where(i => i.CustomerId == customerId).OrderByDescending(i => i.InvoiceDate).ToListAsync();

    public async Task<string> GetNextInvoiceNumberAsync(Guid companyId)
    {
        var nextNumber = await AllocateNextDocumentNumberAsync(companyId, "SalesInvoice");
        return $"INV-{nextNumber:D6}";
    }

    public async Task<string> GetNextCreditNoteNumberAsync(Guid companyId)
    {
        var nextNumber = await AllocateNextDocumentNumberAsync(companyId, "CreditNote");
        return $"CN-{nextNumber:D6}";
    }
}

public class ReceiptRepository : Repository<Receipt>, IReceiptRepository
{
    public ReceiptRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<Receipt?> GetByIdWithDetailsAsync(Guid id) =>
        await _dbSet.Include(r => r.Customer)
                    .Include(r => r.Allocations)
                    .Include(r => r.PostedJournalEntry)
                    .ThenInclude(j => j!.Lines)
                    .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<IReadOnlyList<Receipt>> GetByCustomerAsync(Guid customerId) =>
        await _dbSet.Include(r => r.Allocations).Where(r => r.CustomerId == customerId)
                    .OrderByDescending(r => r.ReceiptDate).ToListAsync();

    public async Task<IReadOnlyList<Receipt>> GetByCompanyAsync(
        Guid companyId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _dbSet.Include(r => r.Customer)
                          .Include(r => r.PostedJournalEntry)
                          .ThenInclude(j => j!.ReversalJournalEntry)
                          .Where(r => r.CompanyId == companyId);
        if (fromDate.HasValue) query = query.Where(r => r.ReceiptDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(r => r.ReceiptDate <= toDate.Value.Date);
        return await query.OrderByDescending(r => r.ReceiptDate).ThenByDescending(r => r.ReceiptNumber).ToListAsync();
    }

    public async Task<string> GetNextReceiptNumberAsync(Guid companyId)
    {
        var nextNumber = await AllocateNextDocumentNumberAsync(companyId, "Receipt");
        return $"RCT-{nextNumber:D6}";
    }
}

public class PurchaseBillRepository : Repository<PurchaseBill>, IPurchaseBillRepository
{
    public PurchaseBillRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<PurchaseBill?> GetByIdWithDetailsAsync(Guid id) =>
        await _dbSet.Include(b => b.Supplier)
                    .Include(b => b.Lines)
                    .Include(b => b.PostedJournalEntry)
                    .ThenInclude(j => j!.Lines)
                     .FirstOrDefaultAsync(b => b.Id == id);

    public async Task AddLineAsync(PurchaseBillLine line) =>
        await _context.PurchaseBillLines.AddAsync(line);

    public async Task<IReadOnlyList<PurchaseBill>> GetByCompanyAsync(Guid companyId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _dbSet.Include(b => b.Supplier)
                          .Include(b => b.Lines)
                          .Include(b => b.PostedJournalEntry)
                          .ThenInclude(j => j!.ReversalJournalEntry)
                          .Where(b => b.CompanyId == companyId);
        if (fromDate.HasValue) query = query.Where(b => b.BillDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(b => b.BillDate <= toDate.Value);
        return await query.OrderByDescending(b => b.BillDate).ToListAsync();
    }

    public async Task<IReadOnlyList<PurchaseBill>> GetBySupplierAsync(Guid supplierId) =>
        await _dbSet.Where(b => b.SupplierId == supplierId).OrderByDescending(b => b.BillDate).ToListAsync();

    public async Task<string> GetNextBillNumberAsync(Guid companyId)
    {
        var nextNumber = await AllocateNextDocumentNumberAsync(companyId, "PurchaseBill");
        return $"BILL-{nextNumber:D6}";
    }

    public async Task<string> GetNextDebitNoteNumberAsync(Guid companyId)
    {
        var nextNumber = await AllocateNextDocumentNumberAsync(companyId, "DebitNote");
        return $"DN-{nextNumber:D6}";
    }
}

public class PaymentRepository : Repository<Payment>, IPaymentRepository
{
    public PaymentRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<Payment?> GetByIdWithDetailsAsync(Guid id) =>
        await _dbSet.Include(p => p.Supplier)
                    .Include(p => p.Allocations)
                    .Include(p => p.PostedJournalEntry)
                    .ThenInclude(j => j!.Lines)
                    .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IReadOnlyList<Payment>> GetBySupplierAsync(Guid supplierId) =>
        await _dbSet.Include(p => p.Allocations).Where(p => p.SupplierId == supplierId)
                    .OrderByDescending(p => p.PaymentDate).ToListAsync();

    public async Task<IReadOnlyList<Payment>> GetByCompanyAsync(
        Guid companyId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _dbSet.Include(p => p.Supplier)
                          .Include(p => p.PostedJournalEntry)
                          .ThenInclude(j => j!.ReversalJournalEntry)
                          .Where(p => p.CompanyId == companyId);
        if (fromDate.HasValue) query = query.Where(p => p.PaymentDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(p => p.PaymentDate <= toDate.Value.Date);
        return await query.OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.PaymentNumber).ToListAsync();
    }

    public async Task<string> GetNextPaymentNumberAsync(Guid companyId)
    {
        var nextNumber = await AllocateNextDocumentNumberAsync(companyId, "Payment");
        return $"PAY-{nextNumber:D6}";
    }
}

public class ItemRepository : Repository<Item>, IItemRepository
{
    public ItemRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<Item?> GetByIdWithDetailsAsync(Guid id) =>
        await _dbSet.Include(i => i.Category)
                    .Include(i => i.Unit)
                    .Include(i => i.StockBalances)
                    .ThenInclude(sb => sb.Warehouse)
                    .FirstOrDefaultAsync(i => i.Id == id);

    public async Task<IReadOnlyList<Item>> GetByCompanyAsync(Guid companyId, bool activeOnly = true) =>
        activeOnly
            ? await _dbSet.Include(i => i.Category)
                          .Include(i => i.Unit)
                          .Include(i => i.StockBalances)
                          .ThenInclude(sb => sb.Warehouse)
                          .Where(i => i.CompanyId == companyId && i.IsActive)
                          .ToListAsync()
            : await _dbSet.Include(i => i.Category)
                          .Include(i => i.Unit)
                          .Include(i => i.StockBalances)
                          .ThenInclude(sb => sb.Warehouse)
                          .Where(i => i.CompanyId == companyId)
                          .ToListAsync();

    public async Task<IReadOnlyList<Item>> GetLowStockItemsAsync(Guid companyId) =>
        await _dbSet.Include(i => i.StockBalances)
                    .Where(i => i.CompanyId == companyId && i.IsActive && i.ItemType == Domain.Enums.ItemType.Goods)
                    .Where(i => i.ReorderLevel.HasValue && i.StockBalances.Sum(sb => sb.Quantity) <= i.ReorderLevel.Value)
                    .ToListAsync();
}

public class WarehouseRepository : Repository<Warehouse>, IWarehouseRepository
{
    public WarehouseRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Warehouse>> GetByCompanyAsync(Guid companyId) =>
        await _dbSet.Where(w => w.CompanyId == companyId).ToListAsync();
}

public class UnitRepository : Repository<Unit>, IUnitRepository
{
    public UnitRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Unit>> GetByCompanyAsync(Guid companyId) =>
        await _dbSet.Where(u => u.CompanyId == companyId).ToListAsync();
}

public class ItemCategoryRepository : Repository<ItemCategory>, IItemCategoryRepository
{
    public ItemCategoryRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<IReadOnlyList<ItemCategory>> GetByCompanyAsync(Guid companyId) =>
        await _dbSet.Where(category => category.CompanyId == companyId).ToListAsync();
}

public class StockMovementRepository : Repository<StockMovement>, IStockMovementRepository
{
    public StockMovementRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<IReadOnlyList<StockMovement>> GetByItemAsync(Guid itemId, Guid? warehouseId = null)
    {
        var query = _dbSet.Where(m => m.ItemId == itemId);
        if (warehouseId.HasValue) query = query.Where(m => m.WarehouseId == warehouseId.Value);
        return await query.OrderByDescending(m => m.MovementDate).ToListAsync();
    }

    public async Task<IReadOnlyList<StockMovement>> GetByWarehouseAsync(Guid warehouseId) =>
        await _dbSet.Where(m => m.WarehouseId == warehouseId).OrderByDescending(m => m.MovementDate).ToListAsync();

    public async Task<IReadOnlyList<StockMovement>> GetBySourceDocumentAsync(
        Guid companyId, Guid documentId, string documentType) =>
        await _dbSet.Where(m => m.CompanyId == companyId
                             && m.SourceDocumentId == documentId
                             && m.SourceDocumentType == documentType)
                    .OrderBy(m => m.CreatedAt).ToListAsync();

    public async Task<IReadOnlyList<StockMovement>> GetItemWarehouseChronologyAsync(
        Guid itemId, Guid warehouseId) =>
        await _dbSet.Where(m => m.ItemId == itemId && m.WarehouseId == warehouseId)
                    .OrderBy(m => m.MovementDate).ThenBy(m => m.CreatedAt).ToListAsync();
}

public class StockBalanceRepository : Repository<StockBalance>, IStockBalanceRepository
{
    public StockBalanceRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<StockBalance?> GetByItemWarehouseAsync(Guid itemId, Guid warehouseId) =>
        await _dbSet.Include(sb => sb.Item)
                    .Include(sb => sb.Warehouse)
                    .FirstOrDefaultAsync(sb => sb.ItemId == itemId && sb.WarehouseId == warehouseId);

    public async Task<IReadOnlyList<StockBalance>> GetByCompanyAsync(Guid companyId, Guid? warehouseId = null)
    {
        var query = _dbSet.Include(sb => sb.Item)
                          .ThenInclude(i => i.Unit)
                          .Include(sb => sb.Warehouse)
                          .Where(sb => sb.CompanyId == companyId);

        if (warehouseId.HasValue)
            query = query.Where(sb => sb.WarehouseId == warehouseId.Value);

        return await query.ToListAsync();
    }
}

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<User?> GetByUserNameAsync(string userName)
    {
        var normalized = userName.Trim().ToUpperInvariant();
        return await _dbSet.Include(u => u.UserRoles)
                           .ThenInclude(ur => ur.Role)
                           .FirstOrDefaultAsync(u => u.UserName.ToUpper() == normalized);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var normalized = email.Trim().ToUpperInvariant();
        return await _dbSet.FirstOrDefaultAsync(u => u.Email.ToUpper() == normalized);
    }

    public async Task<User?> GetByIdWithRolesAsync(Guid id) =>
        await _dbSet.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.Id == id);

    public async Task<bool> ExistsByUserNameAsync(string userName)
    {
        var normalized = userName.Trim().ToUpperInvariant();
        return await _dbSet.AnyAsync(u => u.UserName.ToUpper() == normalized);
    }
}

public class AuditLogRepository : IAuditLogRepository
{
    private readonly BatoBuzzDbContext _context;

    public AuditLogRepository(BatoBuzzDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLog auditLog)
    {
        await _context.AuditLogs.AddAsync(auditLog);
    }

    public async Task<IReadOnlyList<AuditLog>> GetByCompanyAsync(Guid companyId, DateTime? fromDate = null, DateTime? toDate = null, int pageSize = 50)
    {
        var query = _context.AuditLogs.Where(a => a.CompanyId == companyId);
        if (fromDate.HasValue) query = query.Where(a => a.Timestamp >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(a => a.Timestamp <= toDate.Value);
        return await query.OrderByDescending(a => a.Timestamp).Take(pageSize).ToListAsync();
    }
}

public class TdsRateRepository : Repository<TdsRate>, ITdsRateRepository
{
    public TdsRateRepository(BatoBuzzDbContext context) : base(context) { }

    public async Task<IReadOnlyList<TdsRate>> GetByCompanyAsync(Guid companyId, bool activeOnly = true)
    {
        var query = _dbSet.Where(r => r.CompanyId == companyId);
        if (activeOnly) query = query.Where(r => r.IsActive);
        return await query.OrderBy(r => r.Name).ToListAsync();
    }
}
