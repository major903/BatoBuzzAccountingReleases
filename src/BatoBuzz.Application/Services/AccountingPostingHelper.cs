using BatoBuzz.Application.Interfaces;
using BatoBuzz.Domain.Common;
using BatoBuzz.Domain.Entities;
using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Application.Services;

internal sealed class AccountingPostingHelper
{
    private readonly IUnitOfWork _unitOfWork;

    public AccountingPostingHelper(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Callers that mutate domain state (balances, statuses) before reaching
    /// CreateAndPostJournalAsync should call this first, since a rejection here
    /// after those mutations already ran leaves entities tracked in memory with
    /// changes a transaction rollback undoes in the database but not in the
    /// change tracker -- the next retry then fails with an unrelated "already
    /// issued"-style error instead of the real period-lock message.
    /// </summary>
    public async Task EnsurePeriodOpenAsync(Guid companyId, DateTime entryDate)
    {
        var company = await _unitOfWork.Companies.GetByIdWithDetailsAsync(companyId)
            ?? throw new InvalidOperationException("Company not found.");
        var existingYearIds = company.FinancialYears.Select(year => year.Id).ToHashSet();
        var financialYear = company.EnsureFinancialYear(entryDate);
        if (!existingYearIds.Contains(financialYear.Id))
            await _unitOfWork.Companies.AddFinancialYearAsync(financialYear);
        await _unitOfWork.SaveChangesAsync();

        if (financialYear.IsClosed)
            throw new InvalidOperationException(
                $"Cannot post to closed financial year '{financialYear.Name}'.");
        if (company.PeriodLockDate.HasValue && entryDate.Date <= company.PeriodLockDate.Value)
            throw new InvalidOperationException(
                $"Cannot post — {entryDate:yyyy-MM-dd} falls on or before the period lock date ({company.PeriodLockDate.Value:yyyy-MM-dd}).");
    }

    public async Task<JournalEntry> CreateAndPostJournalAsync(
        Guid companyId,
        DateTime entryDate,
        VoucherType voucherType,
        Guid userId,
        string? referenceNumber,
        string? narration,
        Guid? branchId,
        IReadOnlyList<PostingLine> lines)
    {
        var effectiveLines = lines
            .Select(line => line with
            {
                DebitAmount = Money.Round(line.DebitAmount),
                CreditAmount = Money.Round(line.CreditAmount)
            })
            .Where(line => line.DebitAmount != 0 || line.CreditAmount != 0)
            .ToList();
        if (effectiveLines.Count < 2)
            throw new InvalidOperationException("A posting requires at least two journal lines.");

        await EnsurePeriodOpenAsync(companyId, entryDate);

        foreach (var line in effectiveLines)
        {
            if (line.DebitAmount < 0 || line.CreditAmount < 0 ||
                (line.DebitAmount > 0 && line.CreditAmount > 0))
                throw new InvalidOperationException("Each posting line must contain one non-negative debit or credit amount.");
            if (line.TaxRate is < 0 or > 100)
                throw new InvalidOperationException("Posting tax rate must be between 0 and 100.");

            var ledger = await _unitOfWork.Ledgers.GetByIdWithAccountGroupAsync(line.LedgerId)
                ?? throw new InvalidOperationException("Posting ledger not found.");
            if (ledger.CompanyId != companyId)
                throw new InvalidOperationException($"Ledger '{ledger.Name}' does not belong to the posting company.");
            if (!ledger.IsActive)
                throw new InvalidOperationException($"Ledger '{ledger.Name}' is inactive.");
        }

        var totalDebit = effectiveLines.Sum(l => l.DebitAmount);
        var totalCredit = effectiveLines.Sum(l => l.CreditAmount);
        if (totalDebit != totalCredit)
            throw new InvalidOperationException($"Posting is unbalanced. Debit: {totalDebit}, Credit: {totalCredit}.");

        var entryNumber = await _unitOfWork.JournalEntries.GetNextEntryNumberAsync(companyId, (int)voucherType);
        var journal = JournalEntry.Create(
            companyId,
            entryDate,
            voucherType,
            entryNumber,
            userId,
            referenceNumber,
            narration,
            branchId);

        foreach (var line in effectiveLines)
        {
            journal.AddLine(
                line.LedgerId,
                line.DebitAmount,
                line.CreditAmount,
                line.Narration,
                taxRate: line.TaxRate,
                taxCode: line.TaxCode);
        }

        await _unitOfWork.JournalEntries.AddAsync(journal);
        await _unitOfWork.SaveChangesAsync();

        var postedJournal = await _unitOfWork.JournalEntries.GetByIdWithLinesAsync(journal.Id)
            ?? throw new InvalidOperationException("Journal entry could not be loaded for posting.");

        postedJournal.Post(userId);
        await _unitOfWork.SaveChangesAsync();

        return postedJournal;
    }

    public async Task<JournalEntry> ReversePostedJournalWithinCurrentTransactionAsync(
        JournalEntry original,
        DateTime reversalDate,
        string reason,
        Guid userId)
    {
        if (original.Status != TransactionStatus.Posted)
            throw new InvalidOperationException("Only posted journal entries can be reversed.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reversal reason is required.");

        reason = reason.Trim();
        if (reason.Length > 500)
            throw new InvalidOperationException("Reversal reason cannot exceed 500 characters.");

        await EnsurePeriodOpenAsync(original.CompanyId, reversalDate);
        var reversal = await CreateAndPostJournalAsync(
            original.CompanyId,
            reversalDate.Date,
            VoucherType.Reversal,
            userId,
            original.EntryNumber,
            $"Reversal of {original.EntryNumber}: {reason}",
            original.BranchId,
            original.Lines.Select(line => new PostingLine(
                line.LedgerId,
                line.CreditAmount,
                line.DebitAmount,
                line.Narration)).ToList());

        original.MarkReversed(reversal.Id, userId, reason);
        await _unitOfWork.SaveChangesAsync();
        return reversal;
    }

    public async Task<Ledger> GetOrCreateSalesLedgerAsync(Guid companyId) =>
        await GetOrCreateLedgerAsync(companyId, "Sales Account", LedgerType.General, "Sales Accounts", AccountType.Income, 15);

    public async Task<Ledger> GetOrCreatePurchaseLedgerAsync(Guid companyId) =>
        await GetOrCreateLedgerAsync(companyId, "Purchase Account", LedgerType.General, "Purchase Accounts", AccountType.CostOfSales, 14);

    public async Task<Ledger> GetOrCreateSalesVatLedgerAsync(Guid companyId) =>
        await GetOrCreateLedgerAsync(companyId, "VAT Payable", LedgerType.Tax, "Duties & Taxes", AccountType.Liability, 16);

    public async Task<Ledger> GetOrCreatePurchaseVatLedgerAsync(Guid companyId) =>
        await GetOrCreateLedgerAsync(companyId, "VAT Receivable", LedgerType.Tax, "Current Assets", AccountType.Asset, 2);

    public async Task<Ledger> GetOrCreateTdsPayableLedgerAsync(Guid companyId) =>
        await GetOrCreateLedgerAsync(companyId, "TDS Payable", LedgerType.Tax, "Duties & Taxes", AccountType.Liability, 16);

    public async Task<Ledger> GetOrCreateInventoryLedgerAsync(Guid companyId) =>
        await GetOrCreateLedgerAsync(companyId, "Stock-in-Hand", LedgerType.Inventory, "Stock-in-Hand", AccountType.Asset, 4);

    public async Task<Ledger> GetOrCreateCostOfSalesLedgerAsync(Guid companyId) =>
        await GetOrCreateLedgerAsync(companyId, "Cost of Goods Sold", LedgerType.General, "Purchase Accounts", AccountType.CostOfSales, 14);

    public async Task<Ledger> GetOrCreateInventoryWriteOffLedgerAsync(Guid companyId) =>
        await GetOrCreateLedgerAsync(companyId, "Inventory Write-off", LedgerType.General, "Direct Expenses", AccountType.Expense, 12);

    public async Task<Ledger> GetOrCreateOpeningBalanceLedgerAsync(Guid companyId) =>
        await GetOrCreateLedgerAsync(companyId, "Opening Balance Equity", LedgerType.General, "Capital Account", AccountType.Equity, 6);

    public async Task<Ledger> GetOrCreateStockAdjustmentLedgerAsync(Guid companyId) =>
        await GetOrCreateLedgerAsync(companyId, "Stock Adjustment", LedgerType.General, "Direct Expenses", AccountType.Expense, 12);

    public async Task<Ledger> GetOrCreateSettlementLedgerAsync(Guid companyId, PaymentMethod paymentMethod, string? bankName = null)
    {
        if (paymentMethod == PaymentMethod.Cash)
            return await GetOrCreateLedgerAsync(companyId, "Cash Account", LedgerType.Cash, "Cash & Bank", AccountType.Asset, 3);

        var ledgerName = string.IsNullOrWhiteSpace(bankName) ? "Bank Account" : bankName.Trim();
        return await GetOrCreateLedgerAsync(companyId, ledgerName, LedgerType.Bank, "Cash & Bank", AccountType.Asset, 3);
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
        var existingLedger = ledgers.FirstOrDefault(l =>
            string.Equals(l.Name, ledgerName, StringComparison.OrdinalIgnoreCase));

        if (existingLedger != null)
            return existingLedger;

        var company = await _unitOfWork.Companies.GetByIdWithDetailsAsync(companyId)
            ?? throw new InvalidOperationException("Company not found.");

        var group = company.AccountGroups.FirstOrDefault(g =>
            string.Equals(g.Name, groupName, StringComparison.OrdinalIgnoreCase));

        if (group == null)
        {
            group = AccountGroup.Create(companyId, groupName, accountType, displayOrder, isSystem: true);
            company.AccountGroups.Add(group);
        }

        var ledger = Ledger.Create(companyId, group.Id, ledgerName, ledgerType, isSystem: true);
        await _unitOfWork.Ledgers.AddAsync(ledger);
        await _unitOfWork.SaveChangesAsync();

        return await _unitOfWork.Ledgers.GetByIdWithAccountGroupAsync(ledger.Id)
            ?? throw new InvalidOperationException($"Ledger '{ledgerName}' could not be loaded after creation.");
    }
}

internal sealed record PostingLine(
    Guid LedgerId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Narration = null,
    decimal? TaxRate = null,
    string? TaxCode = null);
