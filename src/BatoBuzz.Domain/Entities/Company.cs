using BatoBuzz.Domain.Common;
using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Represents a business entity using the accounting system.
/// Each company has its own chart of accounts, transactions, and data isolation.
/// </summary>
public class Company : AuditableEntity, IAggregateRoot
{
    public string Name { get; private set; } = null!;
    public string? TradingName { get; private set; }
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public string? Province { get; private set; }
    public string Country { get; private set; } = "NP"; // Nepal default
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Website { get; private set; }
    public string? PanNumber { get; private set; } // Nepal PAN
    public string? VatNumber { get; private set; } // Nepal VAT registration
    public string? CompanyRegNumber { get; private set; }
    public string BaseCurrency { get; private set; } = "NPR";
    public int DecimalPlaces { get; private set; } = 2;
    public DateTime FinancialYearStart { get; private set; }
    public DateTime FinancialYearEnd { get; private set; }
    public string? LogoPath { get; private set; }
    public bool IsActive { get; private set; } = true;
    public FiscalYearType FiscalYearType { get; private set; } = FiscalYearType.ShrawanToAshad;
    public DateTime? PeriodLockDate { get; private set; } // no transaction may be dated on or before this date

    // Navigation
    public ICollection<Branch> Branches { get; private set; } = new List<Branch>();
    public ICollection<FinancialYear> FinancialYears { get; private set; } = new List<FinancialYear>();
    public ICollection<AccountGroup> AccountGroups { get; private set; } = new List<AccountGroup>();
    public ICollection<Ledger> Ledgers { get; private set; } = new List<Ledger>();
    public ICollection<JournalEntry> JournalEntries { get; private set; } = new List<JournalEntry>();
    public ICollection<Customer> Customers { get; private set; } = new List<Customer>();
    public ICollection<Supplier> Suppliers { get; private set; } = new List<Supplier>();
    public ICollection<Item> Items { get; private set; } = new List<Item>();
    public ICollection<Warehouse> Warehouses { get; private set; } = new List<Warehouse>();
    public ICollection<ItemCategory> ItemCategories { get; private set; } = new List<ItemCategory>();
    public ICollection<Unit> Units { get; private set; } = new List<Unit>();

    private Company() { } // EF Core protected constructor

    public static Company Create(
        string name,
        DateTime financialYearStart,
        DateTime financialYearEnd,
        Guid createdByUserId,
        string? tradingName = null,
        string? address = null,
        string? city = null,
        string? province = null,
        string? phone = null,
        string? email = null,
        string? panNumber = null,
        string? vatNumber = null,
        string? companyRegNumber = null,
        string baseCurrency = "NPR")
    {
        var company = new Company
        {
            Name = name,
            TradingName = tradingName,
            Address = address,
            City = city,
            Province = province,
            Phone = phone,
            Email = email,
            PanNumber = panNumber,
            VatNumber = vatNumber,
            CompanyRegNumber = companyRegNumber,
            BaseCurrency = baseCurrency,
            FinancialYearStart = financialYearStart,
            FinancialYearEnd = financialYearEnd,
            CreatedByUserId = createdByUserId
        };

        // Create default branch
        var defaultBranch = Branch.CreateDefault(company.Id, name, address, city);
        company.Branches.Add(defaultBranch);

        // Create default financial year
        var fy = FinancialYear.Create(company.Id, financialYearStart, financialYearEnd, isCurrent: true);
        company.FinancialYears.Add(fy);

        return company;
    }

    public void Update(string? name = null, string? address = null, string? phone = null, string? email = null)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name;
        if (address != null) Address = address;
        if (phone != null) Phone = phone;
        if (email != null) Email = email;
    }

    public void UpdateDetails(
        string name,
        string baseCurrency,
        string? tradingName = null,
        string? address = null,
        string? city = null,
        string? province = null,
        string? phone = null,
        string? email = null,
        string? panNumber = null,
        string? vatNumber = null,
        string? companyRegNumber = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Company name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(baseCurrency))
            throw new ArgumentException("Base currency is required.", nameof(baseCurrency));

        Name = name.Trim();
        BaseCurrency = baseCurrency.Trim().ToUpperInvariant();
        TradingName = NormalizeOptional(tradingName);
        Address = NormalizeOptional(address);
        City = NormalizeOptional(city);
        Province = NormalizeOptional(province);
        Phone = NormalizeOptional(phone);
        Email = NormalizeOptional(email);
        PanNumber = NormalizeOptional(panNumber);
        VatNumber = NormalizeOptional(vatNumber);
        CompanyRegNumber = NormalizeOptional(companyRegNumber);
    }

    public void Deactivate() => IsActive = false;

    public void SetPeriodLockDate(DateTime? lockDate) => PeriodLockDate = lockDate?.Date;

    public FinancialYear EnsureFinancialYear(DateTime date)
    {
        var businessDate = date.Date;
        var financialYear = FinancialYears.FirstOrDefault(fy => fy.ContainsDate(businessDate));

        if (financialYear == null)
        {
            var period = FiscalYearType switch
            {
                FiscalYearType.CalendarYear =>
                    (new DateTime(businessDate.Year, 1, 1), new DateTime(businessDate.Year, 12, 31)),
                FiscalYearType.ShrawanToAshad =>
                    BikramSambatConverter.GetNepaliFiscalYearPeriod(businessDate),
                _ => throw new InvalidOperationException($"Unsupported fiscal year type: {FiscalYearType}.")
            };

            financialYear = FinancialYear.Create(
                Id,
                period.Item1,
                period.Item2,
                isCurrent: false);
            FinancialYears.Add(financialYear);
        }

        if (financialYear.StartDate.Date > FinancialYearStart.Date)
        {
            foreach (var year in FinancialYears)
                year.IsCurrent = year.Id == financialYear.Id;

            financialYear.IsCurrent = true;
            FinancialYearStart = financialYear.StartDate.Date;
            FinancialYearEnd = financialYear.EndDate.Date;
        }

        return financialYear;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
