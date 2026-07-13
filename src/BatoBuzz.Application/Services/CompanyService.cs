using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Contracts.Responses;
using BatoBuzz.Domain.Entities;
using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Application.Services;

public class CompanyService : ICompanyService
{
    private readonly IUnitOfWork _unitOfWork;

    public CompanyService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CompanyDto> CreateCompanyAsync(CreateCompanyRequest request, Guid userId)
    {
        if (userId == Guid.Empty)
            throw new InvalidOperationException("A valid owner is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Company name is required.");
        if (request.FinancialYearStart == default || request.FinancialYearEnd == default)
            throw new InvalidOperationException("Financial year dates are required.");
        if (request.FinancialYearEnd.Date < request.FinancialYearStart.Date)
            throw new InvalidOperationException("Financial year end cannot be before its start.");
        if (string.IsNullOrWhiteSpace(request.BaseCurrency))
            throw new InvalidOperationException("Base currency is required.");

        var companyName = request.Name.Trim();
        if (await _unitOfWork.Companies.ExistsByNameAsync(userId, companyName))
            throw new InvalidOperationException($"A company with name '{companyName}' already exists.");

        var company = Company.Create(
            companyName, request.FinancialYearStart, request.FinancialYearEnd, userId,
            request.TradingName, request.Address, request.City, request.Province,
            request.Phone, request.Email, request.PanNumber, request.VatNumber,
            request.CompanyRegNumber, request.BaseCurrency.Trim().ToUpperInvariant());

        // Create default system account groups
        await CreateDefaultAccountGroups(company);
        await CreateDefaultLedgers(company);

        await _unitOfWork.Companies.AddAsync(company);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(company);
    }

    public async Task<CompanyDto> UpdateCompanyAsync(Guid companyId, CreateCompanyRequest request, Guid userId)
    {
        if (companyId == Guid.Empty)
            throw new InvalidOperationException("Company is required.");
        if (userId == Guid.Empty)
            throw new InvalidOperationException("A valid modifying user is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Company name is required.");
        if (string.IsNullOrWhiteSpace(request.BaseCurrency))
            throw new InvalidOperationException("Base currency is required.");

        var name = request.Name.Trim();
        if (name.Length > 200 || request.TradingName?.Trim().Length > 200)
            throw new InvalidOperationException("Company and trading names cannot exceed 200 characters.");
        if (request.Address?.Trim().Length > 500
            || request.City?.Trim().Length > 100
            || request.Province?.Trim().Length > 100
            || request.Phone?.Trim().Length > 50
            || request.Email?.Trim().Length > 100
            || request.PanNumber?.Trim().Length > 50
            || request.VatNumber?.Trim().Length > 50
            || request.CompanyRegNumber?.Trim().Length > 100
            || request.BaseCurrency.Trim().Length > 10)
            throw new InvalidOperationException("One or more company fields exceed the supported length.");

        var company = await _unitOfWork.Companies.GetByIdWithDetailsAsync(companyId)
            ?? throw new InvalidOperationException("Company not found.");
        if (company.CreatedByUserId != userId)
            throw new UnauthorizedAccessException("You do not have access to this company.");
        if (!string.Equals(company.Name, name, StringComparison.OrdinalIgnoreCase)
            && await _unitOfWork.Companies.ExistsByNameAsync(
                company.CreatedByUserId ?? throw new InvalidOperationException("The company owner record is missing."),
                name))
            throw new InvalidOperationException($"A company with name '{name}' already exists.");

        company.UpdateDetails(
            name, request.BaseCurrency, request.TradingName, request.Address,
            request.City, request.Province, request.Phone, request.Email,
            request.PanNumber, request.VatNumber, request.CompanyRegNumber);
        company.SetModifiedBy(userId);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(company);
    }

    public async Task<CompanyDto?> GetCompanyAsync(Guid companyId)
    {
        var company = await _unitOfWork.Companies.GetByIdWithDetailsAsync(companyId);
        return company == null ? null : MapToDto(company);
    }

    public async Task<IReadOnlyList<CompanyDto>> GetUserCompaniesAsync(Guid userId)
    {
        var companies = await _unitOfWork.Companies.GetAllAsync();
        return companies.Where(c => c.IsActive && c.CreatedByUserId == userId).Select(MapToDto).ToList();
    }

    public async Task<FinancialYearDto?> GetCurrentFinancialYearAsync(Guid companyId)
    {
        var company = await _unitOfWork.Companies.GetByIdWithDetailsAsync(companyId);
        if (company == null)
            return null;

        var existingYearIds = company.FinancialYears.Select(year => year.Id).ToHashSet();
        var financialYear = company.EnsureFinancialYear(DateTime.Today);
        if (!existingYearIds.Contains(financialYear.Id))
            await _unitOfWork.Companies.AddFinancialYearAsync(financialYear);
        await _unitOfWork.SaveChangesAsync();
        var current = company.FinancialYears.FirstOrDefault(year => year.IsCurrent);
        return current == null ? null : MapToDto(current);
    }

    public async Task<CompanyDto> SetPeriodLockDateAsync(Guid companyId, DateTime? lockDate, Guid userId)
    {
        var company = await _unitOfWork.Companies.GetByIdAsync(companyId)
            ?? throw new InvalidOperationException("Company not found.");
        if (userId == Guid.Empty || company.CreatedByUserId != userId)
            throw new UnauthorizedAccessException("You do not have access to this company.");

        company.SetPeriodLockDate(lockDate);
        company.SetModifiedBy(userId);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(company);
    }

    private static async Task CreateDefaultAccountGroups(Company company)
    {
        var groups = new[]
        {
            AccountGroup.Create(company.Id, "Fixed Assets", AccountType.Asset, 1, isSystem: true),
            AccountGroup.Create(company.Id, "Current Assets", AccountType.Asset, 2, isSystem: true),
            AccountGroup.Create(company.Id, "Cash & Bank", AccountType.Asset, 3, isSystem: true),
            AccountGroup.Create(company.Id, "Stock-in-Hand", AccountType.Asset, 4, isSystem: true),
            AccountGroup.Create(company.Id, "Sundry Debtors", AccountType.Asset, 5, isSystem: true),
            AccountGroup.Create(company.Id, "Capital Account", AccountType.Equity, 6, isSystem: true),
            AccountGroup.Create(company.Id, "Current Liabilities", AccountType.Liability, 7, isSystem: true),
            AccountGroup.Create(company.Id, "Sundry Creditors", AccountType.Liability, 8, isSystem: true),
            AccountGroup.Create(company.Id, "Loans (Liability)", AccountType.Liability, 9, isSystem: true),
            AccountGroup.Create(company.Id, "Direct Income", AccountType.Income, 10, isSystem: true),
            AccountGroup.Create(company.Id, "Indirect Income", AccountType.OtherIncome, 11, isSystem: true),
            AccountGroup.Create(company.Id, "Direct Expenses", AccountType.Expense, 12, isSystem: true),
            AccountGroup.Create(company.Id, "Indirect Expenses", AccountType.Expense, 13, isSystem: true),
            AccountGroup.Create(company.Id, "Purchase Accounts", AccountType.CostOfSales, 14, isSystem: true),
            AccountGroup.Create(company.Id, "Sales Accounts", AccountType.Income, 15, isSystem: true),
            AccountGroup.Create(company.Id, "Duties & Taxes", AccountType.Liability, 16, isSystem: true),
        };

        foreach (var g in groups)
            company.AccountGroups.Add(g);

        await Task.CompletedTask;
    }

    private static async Task CreateDefaultLedgers(Company company)
    {
        var cashGroup = company.AccountGroups.First(g => g.Name == "Cash & Bank");
        var capitalGroup = company.AccountGroups.First(g => g.Name == "Capital Account");
        var stockGroup = company.AccountGroups.First(g => g.Name == "Stock-in-Hand");

        var ledgers = new[]
        {
            Ledger.Create(company.Id, cashGroup.Id, "Cash Account", LedgerType.Cash, isSystem: true),
            Ledger.Create(company.Id, capitalGroup.Id, "Capital Account", LedgerType.General, isSystem: true),
            Ledger.Create(company.Id, stockGroup.Id, "Stock-in-Hand", LedgerType.Inventory, isSystem: true),
        };

        foreach (var l in ledgers)
            company.Ledgers.Add(l);

        await Task.CompletedTask;
    }

    private static CompanyDto MapToDto(Company c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        TradingName = c.TradingName,
        Address = c.Address,
        City = c.City,
        Province = c.Province,
        Phone = c.Phone,
        Email = c.Email,
        PanNumber = c.PanNumber,
        VatNumber = c.VatNumber,
        CompanyRegNumber = c.CompanyRegNumber,
        BaseCurrency = c.BaseCurrency,
        FinancialYearStart = c.FinancialYearStart,
        FinancialYearEnd = c.FinancialYearEnd,
        IsActive = c.IsActive,
        PeriodLockDate = c.PeriodLockDate,
        Branches = c.Branches.Select(b => new BranchDto
        {
            Id = b.Id, Name = b.Name, Code = b.Code, Address = b.Address, IsActive = b.IsActive, IsDefault = b.IsDefault
        }).ToList(),
        FinancialYears = c.FinancialYears.Select(fy => new FinancialYearDto
        {
            Id = fy.Id, Name = fy.Name, StartDate = fy.StartDate, EndDate = fy.EndDate,
            StartDateBs = fy.StartDateBs, EndDateBs = fy.EndDateBs, IsClosed = fy.IsClosed, IsCurrent = fy.IsCurrent
        }).ToList()
    };

    private static FinancialYearDto MapToDto(FinancialYear fy) => new()
    {
        Id = fy.Id,
        Name = fy.Name,
        StartDate = fy.StartDate,
        EndDate = fy.EndDate,
        StartDateBs = fy.StartDateBs,
        EndDateBs = fy.EndDateBs,
        IsClosed = fy.IsClosed,
        IsCurrent = fy.IsCurrent
    };
}
