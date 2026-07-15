using BatoBuzz.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BatoBuzz.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Name);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.TradingName).HasMaxLength(200);
        builder.Property(e => e.Address).HasMaxLength(500);
        builder.Property(e => e.City).HasMaxLength(100);
        builder.Property(e => e.Province).HasMaxLength(100);
        builder.Property(e => e.Country).HasMaxLength(10).HasDefaultValue("NP");
        builder.Property(e => e.Phone).HasMaxLength(50);
        builder.Property(e => e.Email).HasMaxLength(100);
        builder.Property(e => e.PanNumber).HasMaxLength(50);
        builder.Property(e => e.VatNumber).HasMaxLength(50);
        builder.Property(e => e.CompanyRegNumber).HasMaxLength(100);
        builder.Property(e => e.BaseCurrency).HasMaxLength(10).HasDefaultValue("NPR");
        builder.Property(e => e.DecimalPlaces).HasDefaultValue(2);
        builder.Property(e => e.PeriodLockDate);
        builder.HasMany(e => e.Branches).WithOne().HasForeignKey(e => e.CompanyId);
        builder.HasMany(e => e.FinancialYears).WithOne().HasForeignKey(e => e.CompanyId);
        builder.HasMany(e => e.AccountGroups).WithOne().HasForeignKey(e => e.CompanyId);
        builder.HasMany(e => e.Ledgers).WithOne().HasForeignKey(e => e.CompanyId);
        builder.HasMany(e => e.Customers).WithOne().HasForeignKey(e => e.CompanyId);
        builder.HasMany(e => e.Suppliers).WithOne().HasForeignKey(e => e.CompanyId);
        builder.HasMany(e => e.Items).WithOne().HasForeignKey(e => e.CompanyId);
        builder.HasMany(e => e.Warehouses).WithOne().HasForeignKey(e => e.CompanyId);
        builder.HasMany(e => e.ItemCategories).WithOne().HasForeignKey(e => e.CompanyId);
        builder.HasMany(e => e.Units).WithOne().HasForeignKey(e => e.CompanyId);
    }
}

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(50);
    }
}

public class FinancialYearConfiguration : IEntityTypeConfiguration<FinancialYear>
{
    public void Configure(EntityTypeBuilder<FinancialYear> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(50).IsRequired();
        builder.Property(e => e.StartDateBs).HasMaxLength(20);
        builder.Property(e => e.EndDateBs).HasMaxLength(20);
    }
}

public class AccountGroupConfiguration : IEntityTypeConfiguration<AccountGroup>
{
    public void Configure(EntityTypeBuilder<AccountGroup> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.NameNepali).HasMaxLength(200);
        builder.HasOne(e => e.ParentGroup).WithMany(e => e.ChildGroups).HasForeignKey(e => e.ParentGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class LedgerConfiguration : IEntityTypeConfiguration<Ledger>
{
    public void Configure(EntityTypeBuilder<Ledger> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.CompanyId, e.Name });
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.NameNepali).HasMaxLength(200);
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.Property(e => e.OpeningBalance).HasPrecision(18, 2);
        builder.Property(e => e.CurrentBalance).HasPrecision(18, 2);
        builder.Property(e => e.BankAccountNumber).HasMaxLength(100);
        builder.Property(e => e.BankName).HasMaxLength(200);
        builder.Property(e => e.BankBranch).HasMaxLength(200);
        builder.HasOne(e => e.AccountGroup).WithMany(g => g.Ledgers).HasForeignKey(e => e.AccountGroupId);
    }
}

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.CompanyId, e.EntryNumber }).IsUnique();
        builder.HasIndex(e => e.EntryDate);
        builder.Property(e => e.EntryNumber).HasMaxLength(50).IsRequired();
        builder.Property(e => e.EntryDateBs).HasMaxLength(20);
        builder.Property(e => e.ReferenceNumber).HasMaxLength(100);
        builder.Property(e => e.Narration).HasMaxLength(1000);
        builder.Property(e => e.TotalDebit).HasPrecision(18, 2);
        builder.Property(e => e.TotalCredit).HasPrecision(18, 2);
        builder.Property(e => e.ReversalReason).HasMaxLength(500);
        builder.HasOne(e => e.ReversalJournalEntry)
            .WithMany()
            .HasForeignKey(e => e.ReversedJournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Lines).WithOne(l => l.JournalEntry).HasForeignKey(l => l.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.DebitAmount).HasPrecision(18, 2);
        builder.Property(e => e.CreditAmount).HasPrecision(18, 2);
        builder.Property(e => e.Narration).HasMaxLength(500);
        builder.Property(e => e.CostCentre).HasMaxLength(100);
        builder.Property(e => e.TaxRate).HasPrecision(18, 4);
        builder.Property(e => e.TaxAmount).HasPrecision(18, 2);
        builder.Property(e => e.TaxCode).HasMaxLength(50);
        builder.Property(e => e.IsCleared);
        builder.Property(e => e.ClearedDate);
        builder.HasOne(e => e.Ledger).WithMany(l => l.JournalLines).HasForeignKey(e => e.LedgerId);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.CompanyId, e.Name });
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.NameNepali).HasMaxLength(200);
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.Property(e => e.PanNumber).HasMaxLength(50);
        builder.Property(e => e.VatNumber).HasMaxLength(50);
        builder.Property(e => e.Address).HasMaxLength(500);
        builder.Property(e => e.City).HasMaxLength(100);
        builder.Property(e => e.Phone).HasMaxLength(50);
        builder.Property(e => e.Email).HasMaxLength(100);
        builder.Property(e => e.CreditLimit).HasPrecision(18, 2);
        builder.Property(e => e.CurrentBalance).HasPrecision(18, 2);
        builder.HasOne(e => e.Ledger).WithMany().HasForeignKey(e => e.LedgerId);
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.CompanyId, e.Name });
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.NameNepali).HasMaxLength(200);
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.Property(e => e.PanNumber).HasMaxLength(50);
        builder.Property(e => e.VatNumber).HasMaxLength(50);
        builder.Property(e => e.Address).HasMaxLength(500);
        builder.Property(e => e.City).HasMaxLength(100);
        builder.Property(e => e.Phone).HasMaxLength(50);
        builder.Property(e => e.Email).HasMaxLength(100);
        builder.Property(e => e.CreditLimit).HasPrecision(18, 2);
        builder.Property(e => e.CurrentBalance).HasPrecision(18, 2);
        builder.HasOne(e => e.Ledger).WithMany().HasForeignKey(e => e.LedgerId);
    }
}

public class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.CompanyId, e.InvoiceNumber }).IsUnique();
        builder.Property(e => e.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(e => e.InvoiceDateBs).HasMaxLength(20);
        builder.Property(e => e.Reference).HasMaxLength(100);
        builder.Property(e => e.Narration).HasMaxLength(1000);
        builder.Property(e => e.SubTotal).HasPrecision(18, 2);
        builder.Property(e => e.DiscountAmount).HasPrecision(18, 2);
        builder.Property(e => e.TaxableAmount).HasPrecision(18, 2);
        builder.Property(e => e.VatAmount).HasPrecision(18, 2);
        builder.Property(e => e.TotalAmount).HasPrecision(18, 2);
        builder.Property(e => e.AmountReceived).HasPrecision(18, 2);
        builder.Property(e => e.BalanceDue).HasPrecision(18, 2);
        builder.Property(e => e.VatRate).HasPrecision(5, 2);
        builder.HasOne(e => e.Customer).WithMany(c => c.Invoices).HasForeignKey(e => e.CustomerId);
        builder.HasMany(e => e.Lines).WithOne(l => l.SalesInvoice).HasForeignKey(l => l.SalesInvoiceId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PurchaseBillConfiguration : IEntityTypeConfiguration<PurchaseBill>
{
    public void Configure(EntityTypeBuilder<PurchaseBill> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.CompanyId, e.BillNumber }).IsUnique();
        builder.Property(e => e.BillNumber).HasMaxLength(50).IsRequired();
        builder.Property(e => e.BillDateBs).HasMaxLength(20);
        builder.Property(e => e.SupplierInvoiceNumber).HasMaxLength(100);
        builder.Property(e => e.Reference).HasMaxLength(100);
        builder.Property(e => e.Narration).HasMaxLength(1000);
        builder.Property(e => e.SubTotal).HasPrecision(18, 2);
        builder.Property(e => e.DiscountAmount).HasPrecision(18, 2);
        builder.Property(e => e.TaxableAmount).HasPrecision(18, 2);
        builder.Property(e => e.VatAmount).HasPrecision(18, 2);
        builder.Property(e => e.TotalAmount).HasPrecision(18, 2);
        builder.Property(e => e.AmountPaid).HasPrecision(18, 2);
        builder.Property(e => e.BalanceDue).HasPrecision(18, 2);
        builder.HasOne(e => e.Supplier).WithMany(s => s.Bills).HasForeignKey(e => e.SupplierId);
        builder.HasMany(e => e.Lines).WithOne(l => l.PurchaseBill).HasForeignKey(l => l.PurchaseBillId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.CompanyId, e.Name });
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.NameNepali).HasMaxLength(200);
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.Property(e => e.Barcode).HasMaxLength(100);
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.StandardCost).HasPrecision(18, 4);
        builder.Property(e => e.SalePrice).HasPrecision(18, 4);
        builder.Property(e => e.ReorderLevel).HasPrecision(18, 4);
        builder.Property(e => e.ReorderQuantity).HasPrecision(18, 4);
        builder.HasOne(e => e.Category).WithMany(c => c.Items).HasForeignKey(e => e.CategoryId);
        builder.HasOne(e => e.Unit).WithMany(u => u.Items).HasForeignKey(e => e.UnitId);
    }
}

public class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.ItemId, e.WarehouseId }).IsUnique();
        builder.Property(e => e.Quantity).HasPrecision(18, 4);
        builder.Property(e => e.AverageCost).HasPrecision(18, 4);
        builder.Property(e => e.TotalValue).HasPrecision(18, 2);
    }
}

public class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.HasKey(e => new { e.CompanyId, e.SequenceKey });
        builder.Property(e => e.SequenceKey).HasMaxLength(32);
        builder.Property(e => e.LastValue).IsRequired();
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.UserName).IsUnique();
        builder.HasIndex(e => e.Email).IsUnique();
        builder.Property(e => e.UserName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(200).IsRequired();
        builder.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(e => e.FullName).HasMaxLength(200);
        builder.Property(e => e.Phone).HasMaxLength(50);
        builder.Property(e => e.PreferredLanguage).HasMaxLength(10).HasDefaultValue("en");
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Name).IsUnique();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
    }
}
