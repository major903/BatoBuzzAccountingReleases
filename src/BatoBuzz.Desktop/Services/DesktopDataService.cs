using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Domain.Common;
using BatoBuzz.Domain.Entities;
using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Desktop.Services;

public sealed class DesktopDataService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyService _companyService;

    public DesktopDataService(IUnitOfWork unitOfWork, ICompanyService companyService)
    {
        _unitOfWork = unitOfWork;
        _companyService = companyService;
    }

    public async Task<bool> HasUsersAsync() =>
        await _unitOfWork.Users.CountAsync() > 0;

    public async Task<User?> GetUserByIdAsync(Guid userId) =>
        await _unitOfWork.Users.GetByIdAsync(userId);
    public async Task<Company?> GetOwnedCompanyAsync(
        Guid userId,
        Guid? preferredCompanyId = null)
    {
        if (userId == Guid.Empty)
            throw new InvalidOperationException("A valid user is required.");

        var companies = await _unitOfWork.Companies.GetAllAsync();
        return (preferredCompanyId.HasValue
            ? companies.FirstOrDefault(company =>
                company.IsActive
                && company.CreatedByUserId == userId
                && company.Id == preferredCompanyId.Value)
            : null) ?? companies.FirstOrDefault(company =>
                company.IsActive && company.CreatedByUserId == userId);
    }


    public async Task<Company> EnsureCompanyAsync(Guid userId, Guid? preferredCompanyId = null)
    {
        var existing = await GetOwnedCompanyAsync(userId, preferredCompanyId);
        if (existing != null)
        {
            await EnsureOperationalLedgersAsync(existing.Id);
            return existing;
        }

        var today = DateTime.Today;
        var fyStart = BikramSambatConverter.GetCurrentNepaliFiscalYearStart(today);
        var request = new CreateCompanyRequest
        {
            Name = "Demo Company",
            TradingName = "Demo Company",
            City = "Kathmandu",
            Province = "Bagmati",
            BaseCurrency = "NPR",
            FinancialYearStart = fyStart,
            FinancialYearEnd = fyStart.AddYears(1).AddDays(-1)
        };

        var dto = await _companyService.CreateCompanyAsync(request, userId);
        await EnsureOperationalLedgersAsync(dto.Id);

        return await _unitOfWork.Companies.GetByIdWithDetailsAsync(dto.Id)
            ?? throw new InvalidOperationException("Demo company could not be loaded.");
    }

    public async Task<Customer> GetOrCreateCustomerAsync(Guid companyId, string name, Guid userId)
    {
        var cleanName = string.IsNullOrWhiteSpace(name) ? "Walk-in Customer" : name.Trim();
        var customers = await _unitOfWork.Customers.GetByCompanyAsync(companyId, activeOnly: false);
        var existing = customers.FirstOrDefault(c => string.Equals(c.Name, cleanName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        var debtorsGroup = await GetOrCreateAccountGroupAsync(companyId, "Sundry Debtors", AccountType.Asset, 5);
        var ledger = Ledger.Create(companyId, debtorsGroup.Id, cleanName, LedgerType.Customer, isSystem: false);
        await _unitOfWork.Ledgers.AddAsync(ledger);

        var customer = Customer.Create(companyId, cleanName, ledger.Id);
        customer.SetCreatedBy(userId);
        await _unitOfWork.Customers.AddAsync(customer);
        await _unitOfWork.SaveChangesAsync();
        return customer;
    }

    public async Task<IReadOnlyList<Customer>> GetCustomersAsync(Guid companyId, string? searchText = null)
    {
        var customers = await _unitOfWork.Customers.GetByCompanyAsync(companyId, activeOnly: false);
        if (string.IsNullOrWhiteSpace(searchText))
            return customers;

        return customers
            .Where(c => c.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                || (c.Phone?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                || (c.City?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
    }

    public async Task<IReadOnlyList<Supplier>> GetSuppliersAsync(Guid companyId, string? searchText = null)
    {
        var suppliers = await _unitOfWork.Suppliers.GetByCompanyAsync(companyId, activeOnly: false);
        if (string.IsNullOrWhiteSpace(searchText))
            return suppliers;

        return suppliers
            .Where(s => s.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                || (s.Phone?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                || (s.City?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
    }

    public async Task<Customer> CreateCustomerAsync(
        Guid companyId,
        string name,
        string? address,
        string? city,
        string? phone,
        string? email,
        string? panNumber,
        decimal creditLimit,
        Guid userId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Customer name is required.");
        ValidateContact(name, address, city, phone, email, panNumber, creditLimit);

        if (await _unitOfWork.Customers.ExistsByNameAsync(companyId, name.Trim()))
            throw new InvalidOperationException($"Customer '{name}' already exists.");

        var debtorsGroup = await GetOrCreateAccountGroupAsync(companyId, "Sundry Debtors", AccountType.Asset, 5);
        var ledger = Ledger.Create(companyId, debtorsGroup.Id, name.Trim(), LedgerType.Customer, isSystem: false);
        await _unitOfWork.Ledgers.AddAsync(ledger);

        var customer = Customer.Create(
            companyId,
            name.Trim(),
            ledger.Id,
            panNumber: panNumber,
            address: address,
            city: city,
            phone: phone,
            email: email,
            creditLimit: creditLimit);
        customer.SetCreatedBy(userId);
        await _unitOfWork.Customers.AddAsync(customer);
        await _unitOfWork.SaveChangesAsync();
        return customer;
    }

    public async Task<Customer> UpdateCustomerAsync(
        Guid customerId,
        string name,
        string? address,
        string? city,
        string? phone,
        string? email,
        string? panNumber,
        decimal creditLimit,
        Guid userId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Customer name is required.");
        ValidateContact(name, address, city, phone, email, panNumber, creditLimit);
        var customer = await _unitOfWork.Customers.GetByIdWithLedgerAsync(customerId)
            ?? throw new InvalidOperationException("Customer not found.");
        var normalizedName = name.Trim();
        var duplicates = await _unitOfWork.Customers.GetByCompanyAsync(customer.CompanyId, activeOnly: false);
        if (duplicates.Any(candidate => candidate.Id != customer.Id
            && string.Equals(candidate.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Customer '{normalizedName}' already exists.");

        customer.Update(
            string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            creditLimit,
            panNumber?.Trim());
        customer.SetModifiedBy(userId);
        customer.Ledger.Update(normalizedName);
        customer.Ledger.SetModifiedBy(userId);
        await _unitOfWork.SaveChangesAsync();
        return customer;
    }

    public async Task<Supplier> GetOrCreateSupplierAsync(Guid companyId, string name, Guid userId)
    {
        var cleanName = string.IsNullOrWhiteSpace(name) ? "Walk-in Supplier" : name.Trim();
        var suppliers = await _unitOfWork.Suppliers.GetByCompanyAsync(companyId, activeOnly: false);
        var existing = suppliers.FirstOrDefault(s => string.Equals(s.Name, cleanName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        return await CreateSupplierAsync(companyId, cleanName, null, null, null, null, null, 0, userId);
    }

    public async Task<Supplier> CreateSupplierAsync(
        Guid companyId,
        string name,
        string? address,
        string? city,
        string? phone,
        string? email,
        string? panNumber,
        decimal creditLimit,
        Guid userId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Supplier name is required.");
        ValidateContact(name, address, city, phone, email, panNumber, creditLimit);

        if (await _unitOfWork.Suppliers.ExistsByNameAsync(companyId, name.Trim()))
            throw new InvalidOperationException($"Supplier '{name}' already exists.");

        var creditorsGroup = await GetOrCreateAccountGroupAsync(companyId, "Sundry Creditors", AccountType.Liability, 8);
        var ledger = Ledger.Create(companyId, creditorsGroup.Id, name.Trim(), LedgerType.Supplier, isSystem: false);
        await _unitOfWork.Ledgers.AddAsync(ledger);

        var supplier = Supplier.Create(
            companyId,
            name.Trim(),
            ledger.Id,
            panNumber: panNumber,
            address: address,
            city: city,
            phone: phone,
            email: email,
            creditLimit: creditLimit);
        supplier.SetCreatedBy(userId);
        await _unitOfWork.Suppliers.AddAsync(supplier);
        await _unitOfWork.SaveChangesAsync();
        return supplier;
    }

    public async Task<Supplier> UpdateSupplierAsync(
        Guid supplierId,
        string name,
        string? address,
        string? city,
        string? phone,
        string? email,
        string? panNumber,
        decimal creditLimit,
        Guid userId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Supplier name is required.");
        ValidateContact(name, address, city, phone, email, panNumber, creditLimit);
        var supplier = await _unitOfWork.Suppliers.GetByIdWithLedgerAsync(supplierId)
            ?? throw new InvalidOperationException("Supplier not found.");
        var normalizedName = name.Trim();
        var duplicates = await _unitOfWork.Suppliers.GetByCompanyAsync(supplier.CompanyId, activeOnly: false);
        if (duplicates.Any(candidate => candidate.Id != supplier.Id
            && string.Equals(candidate.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Supplier '{normalizedName}' already exists.");

        supplier.Update(
            string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            creditLimit,
            panNumber?.Trim());
        supplier.SetModifiedBy(userId);
        supplier.Ledger.Update(normalizedName);
        supplier.Ledger.SetModifiedBy(userId);
        await _unitOfWork.SaveChangesAsync();
        return supplier;
    }

    public async Task<IReadOnlyList<Item>> GetItemsAsync(Guid companyId) =>
        await _unitOfWork.Items.GetByCompanyAsync(companyId, activeOnly: false);

    private static void ValidateContact(
        string name, string? address, string? city, string? phone,
        string? email, string? panNumber, decimal creditLimit)
    {
        if (name.Trim().Length > 200 || address?.Trim().Length > 500
            || city?.Trim().Length > 100 || phone?.Trim().Length > 50
            || email?.Trim().Length > 100 || panNumber?.Trim().Length > 50)
            throw new InvalidOperationException("One or more contact fields exceed the supported length.");
        if (creditLimit < 0)
            throw new InvalidOperationException("Credit limit cannot be negative.");
    }

    public async Task<Unit> GetOrCreateUnitAsync(Guid companyId, string? name = null)
    {
        var cleanName = string.IsNullOrWhiteSpace(name) ? "Piece" : name.Trim();
        var units = await _unitOfWork.Units.GetByCompanyAsync(companyId);
        var existing = units.FirstOrDefault(u => string.Equals(u.Name, cleanName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        var unit = Unit.Create(companyId, cleanName, cleanName.Length > 4 ? cleanName.Substring(0, 3) : cleanName);
        await _unitOfWork.Units.AddAsync(unit);
        await _unitOfWork.SaveChangesAsync();
        return unit;
    }

    public async Task<Warehouse> GetOrCreateWarehouseAsync(Guid companyId, string? name = null)
    {
        var cleanName = string.IsNullOrWhiteSpace(name) ? "Main Warehouse" : name.Trim();
        var warehouses = await _unitOfWork.Warehouses.GetByCompanyAsync(companyId);
        var existing = warehouses.FirstOrDefault(w => string.Equals(w.Name, cleanName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        var warehouse = Warehouse.Create(companyId, cleanName, "MAIN", isDefault: !warehouses.Any());
        await _unitOfWork.Warehouses.AddAsync(warehouse);
        await _unitOfWork.SaveChangesAsync();
        return warehouse;
    }

    public async Task<ItemCategory> GetOrCreateItemCategoryAsync(Guid companyId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Category name is required.");
        var cleanName = name.Trim();
        var categories = await _unitOfWork.ItemCategories.GetByCompanyAsync(companyId);
        var existing = categories.FirstOrDefault(category =>
            string.Equals(category.Name, cleanName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        var category = ItemCategory.Create(companyId, cleanName);
        await _unitOfWork.ItemCategories.AddAsync(category);
        await _unitOfWork.SaveChangesAsync();
        return category;
    }

    public Task<IReadOnlyList<Unit>> GetUnitsAsync(Guid companyId) =>
        _unitOfWork.Units.GetByCompanyAsync(companyId);

    public Task<IReadOnlyList<Warehouse>> GetWarehousesAsync(Guid companyId) =>
        _unitOfWork.Warehouses.GetByCompanyAsync(companyId);

    public Task<IReadOnlyList<ItemCategory>> GetItemCategoriesAsync(Guid companyId) =>
        _unitOfWork.ItemCategories.GetByCompanyAsync(companyId);

    public async Task SetUnitActiveAsync(Guid unitId, bool isActive, Guid userId)
    {
        var unit = await _unitOfWork.Units.GetByIdAsync(unitId)
            ?? throw new InvalidOperationException("Unit not found.");
        unit.SetActive(isActive);
        unit.SetModifiedBy(userId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task SetWarehouseActiveAsync(Guid warehouseId, bool isActive, Guid userId)
    {
        var warehouse = await _unitOfWork.Warehouses.GetByIdAsync(warehouseId)
            ?? throw new InvalidOperationException("Warehouse not found.");
        warehouse.SetActive(isActive);
        warehouse.SetModifiedBy(userId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task SetItemCategoryActiveAsync(Guid categoryId, bool isActive, Guid userId)
    {
        var category = await _unitOfWork.ItemCategories.GetByIdAsync(categoryId)
            ?? throw new InvalidOperationException("Category not found.");
        category.SetActive(isActive);
        category.SetModifiedBy(userId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<Item?> GetItemByNameAsync(Guid companyId, string name)
    {
        var items = await _unitOfWork.Items.GetByCompanyAsync(companyId, activeOnly: false);
        return items.FirstOrDefault(i => string.Equals(i.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Ledger> GetLedgerByNameAsync(Guid companyId, string ledgerName)
    {
        var ledgers = await _unitOfWork.Ledgers.GetByCompanyAsync(companyId);
        return ledgers.FirstOrDefault(l => string.Equals(l.Name, ledgerName.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Ledger '{ledgerName}' was not found.");
    }

    public async Task<IReadOnlyList<Ledger>> GetLedgersAsync(Guid companyId) =>
        await _unitOfWork.Ledgers.GetByCompanyAsync(companyId);

    public async Task<IReadOnlyList<AccountGroup>> GetAccountGroupsAsync(Guid companyId)
    {
        var company = await _unitOfWork.Companies.GetByIdWithDetailsAsync(companyId)
            ?? throw new InvalidOperationException("Company not found.");
        return company.AccountGroups.OrderBy(group => group.AccountType).ThenBy(group => group.DisplayOrder).ThenBy(group => group.Name).ToList();
    }

    public async Task<AccountGroup> CreateAccountGroupAsync(
        Guid companyId, string name, AccountType accountType, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Account group name is required.");
        if (name.Trim().Length > 200)
            throw new InvalidOperationException("Account group name cannot exceed 200 characters.");

        var company = await _unitOfWork.Companies.GetByIdWithDetailsAsync(companyId)
            ?? throw new InvalidOperationException("Company not found.");
        if (company.AccountGroups.Any(group => string.Equals(group.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Account group '{name.Trim()}' already exists.");

        var displayOrder = company.AccountGroups.Where(group => group.AccountType == accountType)
            .Select(group => group.DisplayOrder).DefaultIfEmpty(0).Max() + 1;
        var group = AccountGroup.Create(companyId, name.Trim(), accountType, displayOrder);
        group.SetCreatedBy(userId);
        company.AccountGroups.Add(group);
        await _unitOfWork.AccountGroups.AddAsync(group);
        await _unitOfWork.SaveChangesAsync();
        return group;
    }

    public async Task<AccountGroup> RenameAccountGroupAsync(Guid companyId, Guid accountGroupId, string name, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Account group name is required.");
        if (name.Trim().Length > 200)
            throw new InvalidOperationException("Account group name cannot exceed 200 characters.");

        var company = await _unitOfWork.Companies.GetByIdWithDetailsAsync(companyId)
            ?? throw new InvalidOperationException("Company not found.");
        var companyGroups = company.AccountGroups.ToList();
        var group = companyGroups.Single(candidate => candidate.Id == accountGroupId);
        if (group.IsSystem)
            throw new InvalidOperationException("System account groups cannot be renamed.");
        if (companyGroups.Any(candidate => candidate.Id != group.Id
            && string.Equals(candidate.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Account group '{name.Trim()}' already exists.");

        group.Rename(name);
        group.SetModifiedBy(userId);
        await _unitOfWork.SaveChangesAsync();
        return group;
    }

    public async Task SetAccountGroupActiveAsync(Guid companyId, Guid accountGroupId, bool isActive, Guid userId)
    {
        var company = await _unitOfWork.Companies.GetByIdWithDetailsAsync(companyId)
            ?? throw new InvalidOperationException("Company not found.");
        var group = company.AccountGroups.SingleOrDefault(candidate => candidate.Id == accountGroupId)
            ?? throw new InvalidOperationException("Account group not found.");
        if (!isActive && group.Ledgers.Any(ledger => ledger.IsActive))
            throw new InvalidOperationException("Deactivate or move the account group's active ledgers before deactivating the group.");

        group.SetActive(isActive);
        group.SetModifiedBy(userId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<Ledger> CreateLedgerAsync(
        Guid companyId,
        Guid accountGroupId,
        string name,
        string? code,
        LedgerType ledgerType,
        Guid userId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Ledger name is required.");
        if (name.Trim().Length > 200 || code?.Trim().Length > 50)
            throw new InvalidOperationException("Ledger name or code exceeds the supported length.");
        if (ledgerType is LedgerType.Customer or LedgerType.Supplier or LedgerType.Inventory)
            throw new InvalidOperationException("Customer, supplier, and inventory control ledgers are created only by their dedicated workflows.");

        var company = await _unitOfWork.Companies.GetByIdWithDetailsAsync(companyId)
            ?? throw new InvalidOperationException("Company not found.");
        if (company.AccountGroups.All(group => group.Id != accountGroupId || !group.IsActive))
            throw new InvalidOperationException("Account group does not belong to the selected company.");
        if (company.Ledgers.Any(ledger => string.Equals(ledger.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Ledger '{name.Trim()}' already exists.");
        if (!string.IsNullOrWhiteSpace(code) && company.Ledgers.Any(ledger => string.Equals(ledger.Code, code.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Ledger code '{code.Trim()}' already exists.");

        var ledger = Ledger.Create(companyId, accountGroupId, name.Trim(), ledgerType, code?.Trim());
        ledger.SetCreatedBy(userId);
        await _unitOfWork.Ledgers.AddAsync(ledger);
        await _unitOfWork.SaveChangesAsync();
        return ledger;
    }

    public async Task<Ledger> UpdateLedgerAsync(
        Guid ledgerId, string name, string? code, bool isActive, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Ledger name is required.");
        if (name.Trim().Length > 200 || code?.Trim().Length > 50)
            throw new InvalidOperationException("Ledger name or code exceeds the supported length.");

        var ledger = await _unitOfWork.Ledgers.GetByIdAsync(ledgerId)
            ?? throw new InvalidOperationException("Ledger not found.");
        if (ledger.IsSystem)
            throw new InvalidOperationException("System ledgers are maintained by their dedicated workflows and cannot be edited here.");
        if (ledger.LedgerType is LedgerType.Customer or LedgerType.Supplier or LedgerType.Inventory)
            throw new InvalidOperationException("Control ledgers are maintained by their dedicated workflows and cannot be edited here.");

        var ledgers = await _unitOfWork.Ledgers.GetByCompanyAsync(ledger.CompanyId);
        if (ledgers.Any(candidate => candidate.Id != ledger.Id
            && string.Equals(candidate.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Ledger '{name.Trim()}' already exists.");
        if (!string.IsNullOrWhiteSpace(code) && ledgers.Any(candidate => candidate.Id != ledger.Id
            && string.Equals(candidate.Code, code.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Ledger code '{code.Trim()}' already exists.");

        ledger.Update(name.Trim(), string.IsNullOrWhiteSpace(code) ? null : code.Trim(), isActive);
        ledger.SetModifiedBy(userId);
        await _unitOfWork.SaveChangesAsync();
        return ledger;
    }

    public async Task<IReadOnlyList<JournalEntry>> GetJournalEntriesAsync(Guid companyId, DateTime fromDate, DateTime toDate) =>
        await _unitOfWork.JournalEntries.GetByCompanyAsync(companyId, fromDate, toDate);

    private async Task EnsureOperationalLedgersAsync(Guid companyId)
    {
        await GetOrCreateLedgerAsync(companyId, "Sales Account", LedgerType.General, "Sales Accounts", AccountType.Income, 15);
        await GetOrCreateLedgerAsync(companyId, "Purchase Account", LedgerType.General, "Purchase Accounts", AccountType.CostOfSales, 14);
        await GetOrCreateLedgerAsync(companyId, "VAT Payable", LedgerType.Tax, "Duties & Taxes", AccountType.Liability, 16);
        await GetOrCreateLedgerAsync(companyId, "VAT Receivable", LedgerType.Tax, "Current Assets", AccountType.Asset, 2);
        await GetOrCreateLedgerAsync(companyId, "Bank Account", LedgerType.Bank, "Cash & Bank", AccountType.Asset, 3);
        await GetOrCreateLedgerAsync(companyId, "Opening Balance Equity", LedgerType.General, "Capital Account", AccountType.Equity, 6);
        await GetOrCreateUnitAsync(companyId);
        await GetOrCreateWarehouseAsync(companyId);
    }


    private async Task<AccountGroup> GetOrCreateAccountGroupAsync(Guid companyId, string name, AccountType accountType, int displayOrder)
    {
        var company = await _unitOfWork.Companies.GetByIdWithDetailsAsync(companyId)
            ?? throw new InvalidOperationException("Company not found.");

        var group = company.AccountGroups.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
        if (group != null)
            return group;

        group = AccountGroup.Create(companyId, name, accountType, displayOrder, isSystem: true);
        company.AccountGroups.Add(group);
        await _unitOfWork.SaveChangesAsync();
        return group;
    }

    private async Task<Ledger> GetOrCreateLedgerAsync(
        Guid companyId,
        string ledgerName,
        LedgerType ledgerType,
        string groupName,
        AccountType accountType,
        int displayOrder)
    {
        var ledgers = await _unitOfWork.Ledgers.GetByCompanyAsync(companyId);
        var existing = ledgers.FirstOrDefault(l => string.Equals(l.Name, ledgerName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        var group = await GetOrCreateAccountGroupAsync(companyId, groupName, accountType, displayOrder);
        var ledger = Ledger.Create(companyId, group.Id, ledgerName, ledgerType, isSystem: true);
        await _unitOfWork.Ledgers.AddAsync(ledger);
        await _unitOfWork.SaveChangesAsync();
        return ledger;
    }
}
