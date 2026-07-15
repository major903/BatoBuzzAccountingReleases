using BatoBuzz.Domain;
using BatoBuzz.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the BatoBuzz Accounting database.
/// Supports both SQLite (local) and PostgreSQL (server) through configuration.
/// </summary>
public class BatoBuzzDbContext : DbContext
{
    public BatoBuzzDbContext(DbContextOptions<BatoBuzzDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<FinancialYear> FinancialYears => Set<FinancialYear>();
    public DbSet<AccountGroup> AccountGroups => Set<AccountGroup>();
    public DbSet<Ledger> Ledgers => Set<Ledger>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceLine> SalesInvoiceLines => Set<SalesInvoiceLine>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptAllocation> ReceiptAllocations => Set<ReceiptAllocation>();
    public DbSet<PurchaseBill> PurchaseBills => Set<PurchaseBill>();
    public DbSet<PurchaseBillLine> PurchaseBillLines => Set<PurchaseBillLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<TdsRate> TdsRates => Set<TdsRate>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations
        modelBuilder.ApplyConfiguration(new Configurations.CompanyConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.BranchConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.FinancialYearConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.AccountGroupConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.LedgerConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.JournalEntryConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.JournalLineConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.SupplierConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.SalesInvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.PurchaseBillConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ItemConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.StockBalanceConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.DocumentSequenceConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.UserConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.RoleConfiguration());

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(entityType => typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType).Property<byte[]>(nameof(AuditableEntity.RowVersion))
                .IsConcurrencyToken().ValueGeneratedNever();
        }

        ConfigureBusinessDateColumns(modelBuilder);

        // Explicit composite keys for join entities. EF Core cannot infer these keys.
        modelBuilder.Entity<UserRole>(builder =>
        {
            builder.HasKey(e => new { e.UserId, e.RoleId });
            builder.HasOne(e => e.User)
                .WithMany(e => e.UserRoles)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(e => e.Role)
                .WithMany(e => e.UserRoles)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RolePermission>(builder =>
        {
            builder.HasKey(e => new { e.RoleId, e.PermissionId });
            builder.HasOne(e => e.Role)
                .WithMany(e => e.RolePermissions)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(e => e.Permission)
                .WithMany(e => e.RolePermissions)
                .HasForeignKey(e => e.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed system data
        SeedSystemData(modelBuilder);
    }

    private static void SeedSystemData(ModelBuilder modelBuilder)
    {
        // Seed default roles
        var seedCreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<Role>().HasData(
            new { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Owner", Description = "Full system access", IsSystem = true, CreatedAt = seedCreatedAt, CompanyId = Guid.Empty, RowVersion = Array.Empty<byte>() },
            new { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Accountant", Description = "Accounting and reporting access", IsSystem = true, CreatedAt = seedCreatedAt, CompanyId = Guid.Empty, RowVersion = Array.Empty<byte>() },
            new { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Sales User", Description = "Sales and customer access", IsSystem = true, CreatedAt = seedCreatedAt, CompanyId = Guid.Empty, RowVersion = Array.Empty<byte>() },
            new { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Name = "Purchase User", Description = "Purchase and supplier access", IsSystem = true, CreatedAt = seedCreatedAt, CompanyId = Guid.Empty, RowVersion = Array.Empty<byte>() },
            new { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), Name = "Cashier", Description = "Cash and receipt access", IsSystem = true, CreatedAt = seedCreatedAt, CompanyId = Guid.Empty, RowVersion = Array.Empty<byte>() },
            new { Id = Guid.Parse("66666666-6666-6666-6666-666666666666"), Name = "Storekeeper", Description = "Inventory management access", IsSystem = true, CreatedAt = seedCreatedAt, CompanyId = Guid.Empty, RowVersion = Array.Empty<byte>() },
            new { Id = Guid.Parse("77777777-7777-7777-7777-777777777777"), Name = "Auditor", Description = "Read-only audit access", IsSystem = true, CreatedAt = seedCreatedAt, CompanyId = Guid.Empty, RowVersion = Array.Empty<byte>() }
        );
    }

    private static void ConfigureBusinessDateColumns(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>().Property(entity => entity.FinancialYearStart).HasColumnType("date");
        modelBuilder.Entity<Company>().Property(entity => entity.FinancialYearEnd).HasColumnType("date");
        modelBuilder.Entity<Company>().Property(entity => entity.PeriodLockDate).HasColumnType("date");
        modelBuilder.Entity<FinancialYear>().Property(entity => entity.StartDate).HasColumnType("date");
        modelBuilder.Entity<FinancialYear>().Property(entity => entity.EndDate).HasColumnType("date");
        modelBuilder.Entity<JournalEntry>().Property(entity => entity.EntryDate).HasColumnType("date");
        modelBuilder.Entity<JournalLine>().Property(entity => entity.ClearedDate).HasColumnType("date");
        modelBuilder.Entity<SalesInvoice>().Property(entity => entity.InvoiceDate).HasColumnType("date");
        modelBuilder.Entity<SalesInvoice>().Property(entity => entity.DueDate).HasColumnType("date");
        modelBuilder.Entity<PurchaseBill>().Property(entity => entity.BillDate).HasColumnType("date");
        modelBuilder.Entity<PurchaseBill>().Property(entity => entity.DueDate).HasColumnType("date");
        modelBuilder.Entity<Receipt>().Property(entity => entity.ReceiptDate).HasColumnType("date");
        modelBuilder.Entity<Receipt>().Property(entity => entity.ChequeDate).HasColumnType("date");
        modelBuilder.Entity<Payment>().Property(entity => entity.PaymentDate).HasColumnType("date");
        modelBuilder.Entity<Payment>().Property(entity => entity.ChequeDate).HasColumnType("date");
        modelBuilder.Entity<StockMovement>().Property(entity => entity.MovementDate).HasColumnType("date");
        modelBuilder.Entity<StockMovement>().Property(entity => entity.ExpiryDate).HasColumnType("date");
    }

    private void StampRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            entry.Property(entity => entity.RowVersion).CurrentValue = Guid.NewGuid().ToByteArray();
            if (entry.State == EntityState.Modified)
                entry.Property(entity => entity.RowVersion).IsModified = true;
        }
    }
}
