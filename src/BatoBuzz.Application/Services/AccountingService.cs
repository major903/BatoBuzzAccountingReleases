using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Common;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Contracts.Responses;
using BatoBuzz.Domain.Entities;
using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Application.Services;

public class AccountingService : IAccountingService
{
    private readonly IUnitOfWork _unitOfWork;

    public AccountingService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<JournalEntryDto> CreateJournalAsync(CreateJournalRequest request, Guid userId)
    {
        if (request.CompanyId == Guid.Empty)
            throw new InvalidOperationException("Company is required.");
        if (request.EntryDate == default)
            throw new InvalidOperationException("Entry date is required.");
        if (!Enum.IsDefined(typeof(VoucherType), request.VoucherType))
            throw new InvalidOperationException("Voucher type is invalid.");
        if (request.Lines.Count < 2)
            throw new InvalidOperationException("A journal requires at least two lines.");

        var company = await _unitOfWork.Companies.GetByIdWithDetailsAsync(request.CompanyId)
            ?? throw new InvalidOperationException("Company not found.");
        if (request.BranchId.HasValue && company.Branches.All(b => b.Id != request.BranchId.Value || !b.IsActive))
            throw new InvalidOperationException("Branch does not belong to the selected company or is inactive.");

        foreach (var line in request.Lines)
        {
            var ledger = await _unitOfWork.Ledgers.GetByIdWithAccountGroupAsync(line.LedgerId)
                ?? throw new InvalidOperationException("Journal ledger not found.");
            if (ledger.CompanyId != request.CompanyId)
                throw new InvalidOperationException($"Ledger '{ledger.Name}' does not belong to the selected company.");
            if (!ledger.IsActive)
                throw new InvalidOperationException($"Ledger '{ledger.Name}' is inactive.");
            if (ledger.LedgerType is LedgerType.Customer or LedgerType.Supplier or LedgerType.Inventory)
                throw new InvalidOperationException($"Control ledger '{ledger.Name}' cannot be used in a manual journal.");
            if (line.DebitAmount > 9_999_999_999_999_999.99m || line.CreditAmount > 9_999_999_999_999_999.99m)
                throw new InvalidOperationException("Journal amount exceeds the supported range.");
            if (line.TaxRate is < 0 or > 100)
                throw new InvalidOperationException("Journal tax rate must be between 0 and 100.");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var entryNumber = await _unitOfWork.JournalEntries.GetNextEntryNumberAsync(request.CompanyId, request.VoucherType);
            var entry = JournalEntry.Create(
                request.CompanyId, request.EntryDate, (VoucherType)request.VoucherType,
                entryNumber, userId, request.ReferenceNumber, request.Narration, request.BranchId);

            foreach (var line in request.Lines)
            {
                entry.AddLine(line.LedgerId, line.DebitAmount, line.CreditAmount,
                    line.Narration, line.CostCentre, line.TaxRate, line.TaxCode);
            }

            await _unitOfWork.JournalEntries.AddAsync(entry);
            await _unitOfWork.SaveChangesAsync();

            var savedEntry = await _unitOfWork.JournalEntries.GetByIdWithLinesAsync(entry.Id)
                ?? throw new InvalidOperationException("Journal entry could not be loaded after creation.");

            await _unitOfWork.CommitTransactionAsync();
            return MapJournalToDto(savedEntry);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<JournalEntryDto> PostJournalAsync(Guid journalId, Guid userId)
    {
        var entry = await _unitOfWork.JournalEntries.GetByIdWithLinesAsync(journalId)
            ?? throw new InvalidOperationException("Journal entry not found.");

        await new AccountingPostingHelper(_unitOfWork)
            .EnsurePeriodOpenAsync(entry.CompanyId, entry.EntryDate);
        if (entry.Lines.Any(l => l.Ledger.CompanyId != entry.CompanyId))
            throw new InvalidOperationException("Journal contains a ledger from another company.");
        if (entry.Lines.Any(l => !l.Ledger.IsActive))
            throw new InvalidOperationException("Journal contains an inactive ledger.");
        if (entry.Lines.Any(l => l.Ledger.LedgerType is LedgerType.Customer or LedgerType.Supplier or LedgerType.Inventory))
            throw new InvalidOperationException("Manual journals cannot post to customer, supplier, or inventory control ledgers.");

        entry.Post(userId);
        await _unitOfWork.SaveChangesAsync();

        return MapJournalToDto(entry);
    }

    public Task<JournalEntryDto> ReverseJournalAsync(Guid journalId, string reason, Guid userId) =>
        ReverseJournalAsync(journalId, new CorrectPostedDocumentRequest
        {
            CorrectionDate = DateTime.Today,
            Reason = reason
        }, userId);

    public async Task<JournalEntryDto> ReverseJournalAsync(
        Guid journalId, CorrectPostedDocumentRequest request, Guid userId)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (request.CorrectionDate == default)
            throw new InvalidOperationException("Correction date is required.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("A reversal reason is required.");
        var reason = request.Reason.Trim();
        if (reason.Length > 500)
            throw new InvalidOperationException("Reversal reason cannot exceed 500 characters.");

        var entry = await _unitOfWork.JournalEntries.GetByIdWithLinesAsync(journalId)
            ?? throw new InvalidOperationException("Journal entry not found.");
        if (entry.Status != TransactionStatus.Posted)
            throw new InvalidOperationException("Only posted journal entries can be reversed.");
        if (request.CorrectionDate.Date < entry.EntryDate.Date)
            throw new InvalidOperationException("Correction date cannot be before the journal date.");
        if (entry.VoucherType is VoucherType.Sales or VoucherType.Purchase
            or VoucherType.Receipt or VoucherType.Payment
            or VoucherType.StockJournal or VoucherType.SalesReturn
            or VoucherType.PurchaseReturn or VoucherType.Reversal)
            throw new InvalidOperationException("Operational vouchers must be reversed through their source document workflow.");

        var reversalDate = request.CorrectionDate.Date;
        var posting = new AccountingPostingHelper(_unitOfWork);
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await posting.ReversePostedJournalWithinCurrentTransactionAsync(
                entry, reversalDate, reason, userId);
            var auditUser = await _unitOfWork.Users.GetByIdAsync(userId);
            await _unitOfWork.AuditLogs.AddAsync(AuditLog.Create(
                "Journal.Reversed", nameof(JournalEntry), entry.Id, entry.CompanyId,
                userId, userName: auditUser?.UserName ?? userId.ToString(), newValues: reason));
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return MapJournalToDto(entry);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<JournalEntryDto>> GetJournalsAsync(
        Guid companyId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        _ = await _unitOfWork.Companies.GetByIdAsync(companyId)
            ?? throw new InvalidOperationException("Company not found.");
        var entries = await _unitOfWork.JournalEntries.GetByCompanyAsync(companyId, fromDate, toDate);
        return entries.Select(MapJournalToDto).ToList();
    }

    public async Task<TrialBalanceReportDto> GetTrialBalanceAsync(Guid companyId, DateTime fromDate, DateTime toDate)
    {
        ValidateDateRange(fromDate, toDate);
        _ = await _unitOfWork.Companies.GetByIdAsync(companyId)
            ?? throw new InvalidOperationException("Company not found.");

        var ledgers = await _unitOfWork.Ledgers.GetByCompanyAsync(companyId);

        var items = new List<TrialBalanceItemDto>();

        foreach (var ledger in ledgers)
        {
            var entries = await _unitOfWork.JournalEntries.GetByLedgerAsync(ledger.Id, DateTime.MinValue, toDate);
            var openingLines = entries.Where(j => j.EntryDate < fromDate)
                .SelectMany(j => j.Lines.Where(l => l.LedgerId == ledger.Id))
                .ToList();
            var periodLines = entries.Where(j => j.EntryDate >= fromDate)
                .SelectMany(j => j.Lines.Where(l => l.LedgerId == ledger.Id))
                .ToList();

            var periodDebit = periodLines.Sum(l => l.DebitAmount);
            var periodCredit = periodLines.Sum(l => l.CreditAmount);

            var signedOpening =
                (ledger.OpeningBalanceType == OpeningBalanceType.Debit ? ledger.OpeningBalance : -ledger.OpeningBalance)
                + openingLines.Sum(l => l.DebitAmount - l.CreditAmount);
            var openingDebit = signedOpening > 0 ? signedOpening : 0;
            var openingCredit = signedOpening < 0 ? Math.Abs(signedOpening) : 0;
            var signedClosing = openingDebit - openingCredit + periodDebit - periodCredit;
            var closingDebit = signedClosing > 0 ? signedClosing : 0;
            var closingCredit = signedClosing < 0 ? Math.Abs(signedClosing) : 0;

            items.Add(new TrialBalanceItemDto
            {
                LedgerId = ledger.Id,
                LedgerName = ledger.Name,
                AccountType = (AccountTypeDto)ledger.AccountGroup.AccountType,
                OpeningDebit = openingDebit,
                OpeningCredit = openingCredit,
                PeriodDebit = periodDebit,
                PeriodCredit = periodCredit,
                ClosingDebit = closingDebit,
                ClosingCredit = closingCredit
            });
        }

        return new TrialBalanceReportDto
        {
            CompanyId = companyId,
            FromDate = fromDate,
            ToDate = toDate,
            Items = items,
            TotalDebit = items.Sum(i => i.ClosingDebit),
            TotalCredit = items.Sum(i => i.ClosingCredit)
        };
    }

    public async Task<ProfitAndLossReportDto> GetProfitAndLossAsync(Guid companyId, DateTime fromDate, DateTime toDate)
    {
        ValidateDateRange(fromDate, toDate);
        _ = await _unitOfWork.Companies.GetByIdAsync(companyId)
            ?? throw new InvalidOperationException("Company not found.");

        var ledgers = await _unitOfWork.Ledgers.GetByCompanyAsync(companyId);
        var incomeAccounts = new List<PLAccountDto>();
        var cosAccounts = new List<PLAccountDto>();
        var expenseAccounts = new List<PLAccountDto>();

        foreach (var ledger in ledgers)
        {
            var lines = (await _unitOfWork.JournalEntries.GetByLedgerAsync(ledger.Id, fromDate, toDate))
                .SelectMany(j => j.Lines.Where(l => l.LedgerId == ledger.Id));

            var totalDebit = lines.Sum(l => l.DebitAmount);
            var totalCredit = lines.Sum(l => l.CreditAmount);

            switch (ledger.AccountGroup.AccountType)
            {
                case AccountType.Income:
                case AccountType.OtherIncome:
                    if (totalCredit != totalDebit) incomeAccounts.Add(new PLAccountDto { LedgerId = ledger.Id, LedgerName = ledger.Name, Amount = totalCredit - totalDebit });
                    break;
                case AccountType.CostOfSales:
                    if (totalDebit != totalCredit) cosAccounts.Add(new PLAccountDto { LedgerId = ledger.Id, LedgerName = ledger.Name, Amount = totalDebit - totalCredit });
                    break;
                case AccountType.Expense:
                case AccountType.OtherExpense:
                    if (totalDebit != totalCredit) expenseAccounts.Add(new PLAccountDto { LedgerId = ledger.Id, LedgerName = ledger.Name, Amount = totalDebit - totalCredit });
                    break;
            }
        }

        var totalIncome = incomeAccounts.Sum(a => a.Amount);
        var totalCos = cosAccounts.Sum(a => a.Amount);
        var totalExpenses = expenseAccounts.Sum(a => a.Amount);

        return new ProfitAndLossReportDto
        {
            CompanyId = companyId,
            FromDate = fromDate,
            ToDate = toDate,
            TotalIncome = totalIncome,
            TotalCostOfSales = totalCos,
            GrossProfit = totalIncome - totalCos,
            TotalExpenses = totalExpenses,
            NetProfit = totalIncome - totalCos - totalExpenses,
            IncomeAccounts = incomeAccounts,
            CostOfSalesAccounts = cosAccounts,
            ExpenseAccounts = expenseAccounts
        };
    }

    public async Task<BalanceSheetReportDto> GetBalanceSheetAsync(Guid companyId, DateTime asOfDate)
    {
        if (asOfDate == default)
            throw new InvalidOperationException("As-of date is required.");
        _ = await _unitOfWork.Companies.GetByIdAsync(companyId)
            ?? throw new InvalidOperationException("Company not found.");

        var ledgers = await _unitOfWork.Ledgers.GetByCompanyAsync(companyId);
        var assets = new List<BSAccountDto>();
        var liabilities = new List<BSAccountDto>();
        var equity = new List<BSAccountDto>();
        decimal cumulativeEarnings = 0;

        foreach (var ledger in ledgers)
        {
            var lines = (await _unitOfWork.JournalEntries.GetByLedgerAsync(ledger.Id, DateTime.MinValue, asOfDate))
                .SelectMany(j => j.Lines.Where(l => l.LedgerId == ledger.Id)).ToList();

            var netDebit = lines.Sum(l => l.DebitAmount) + (ledger.OpeningBalanceType == OpeningBalanceType.Debit ? ledger.OpeningBalance : 0);
            var netCredit = lines.Sum(l => l.CreditAmount) + (ledger.OpeningBalanceType == OpeningBalanceType.Credit ? ledger.OpeningBalance : 0);
            var balance = netDebit - netCredit;

            switch (ledger.AccountGroup.AccountType)
            {
                case AccountType.Asset:
                    if (balance != 0) assets.Add(new BSAccountDto { LedgerId = ledger.Id, LedgerName = ledger.Name, Amount = balance });
                    break;
                case AccountType.Liability:
                    if (balance != 0) liabilities.Add(new BSAccountDto { LedgerId = ledger.Id, LedgerName = ledger.Name, Amount = -balance });
                    break;
                case AccountType.Equity:
                    if (balance != 0) equity.Add(new BSAccountDto { LedgerId = ledger.Id, LedgerName = ledger.Name, Amount = -balance });
                    break;
                case AccountType.Income:
                case AccountType.OtherIncome:
                    cumulativeEarnings += lines.Sum(line => line.CreditAmount - line.DebitAmount);
                    break;
                case AccountType.CostOfSales:
                case AccountType.Expense:
                case AccountType.OtherExpense:
                    cumulativeEarnings -= lines.Sum(line => line.DebitAmount - line.CreditAmount);
                    break;
            }
        }

        if (cumulativeEarnings != 0)
        {
            equity.Add(new BSAccountDto
            {
                LedgerId = Guid.Empty,
                LedgerName = "Cumulative Earnings",
                Amount = cumulativeEarnings
            });
        }

        var totalAssets = assets.Sum(a => a.Amount);
        var totalLiabilities = liabilities.Sum(a => a.Amount);
        var totalEquity = equity.Sum(a => a.Amount);

        return new BalanceSheetReportDto
        {
            CompanyId = companyId,
            AsOfDate = asOfDate,
            TotalAssets = totalAssets,
            TotalLiabilities = totalLiabilities,
            TotalEquity = totalEquity,
            NetAssets = totalAssets - totalLiabilities - totalEquity,
            Assets = assets,
            Liabilities = liabilities,
            Equity = equity
        };
    }

    public async Task<GeneralLedgerReportDto> GetGeneralLedgerAsync(Guid companyId, Guid ledgerId, DateTime fromDate, DateTime toDate)
    {
        ValidateDateRange(fromDate, toDate);

        var ledger = await _unitOfWork.Ledgers.GetByIdWithAccountGroupAsync(ledgerId)
            ?? throw new InvalidOperationException("Ledger not found.");
        if (ledger.CompanyId != companyId)
            throw new InvalidOperationException("Ledger does not belong to the selected company.");

        var entries = await _unitOfWork.JournalEntries.GetByLedgerAsync(ledgerId, DateTime.MinValue, toDate);
        var transactions = new List<GLTransactionDto>();

        var openingBalance = ledger.OpeningBalanceType == OpeningBalanceType.Debit
            ? ledger.OpeningBalance
            : -ledger.OpeningBalance;
        openingBalance += entries.Where(e => e.EntryDate < fromDate)
            .SelectMany(e => e.Lines.Where(l => l.LedgerId == ledgerId))
            .Sum(l => l.DebitAmount - l.CreditAmount);
        var runningBalance = openingBalance;

        foreach (var entry in entries.Where(e => e.EntryDate >= fromDate)
                     .OrderBy(e => e.EntryDate)
                     .ThenBy(e => e.EntryNumber))
        {
            foreach (var line in entry.Lines.Where(l => l.LedgerId == ledgerId))
            {
                runningBalance += line.DebitAmount - line.CreditAmount;
                transactions.Add(new GLTransactionDto
                {
                    LineId = line.Id,
                    Date = entry.EntryDate,
                    VoucherType = entry.VoucherType.ToString(),
                    EntryNumber = entry.EntryNumber,
                    Narration = line.Narration ?? entry.Narration,
                    Debit = line.DebitAmount,
                    Credit = line.CreditAmount,
                    Balance = runningBalance,
                    IsCleared = line.IsCleared,
                    ClearedDate = line.ClearedDate
                });
            }
        }

        return new GeneralLedgerReportDto
        {
            CompanyId = companyId,
            LedgerId = ledgerId,
            LedgerName = ledger.Name,
            FromDate = fromDate,
            ToDate = toDate,
            OpeningBalance = openingBalance,
            Transactions = transactions,
            ClosingBalance = runningBalance
        };
    }

    public async Task SetLineClearedAsync(Guid journalLineId, bool cleared, DateTime? clearedDate, Guid userId)
    {
        var line = await _unitOfWork.JournalEntries.GetLineByIdAsync(journalLineId)
            ?? throw new InvalidOperationException("Journal line not found.");
        if (line.JournalEntry.Status != TransactionStatus.Posted)
            throw new InvalidOperationException("Only posted journal lines can be reconciled.");

        line.SetCleared(cleared, clearedDate);
        line.SetModifiedBy(userId);
        await _unitOfWork.SaveChangesAsync();
    }

    private static void ValidateDateRange(DateTime fromDate, DateTime toDate)
    {
        if (fromDate == default || toDate == default)
            throw new InvalidOperationException("Both from and to dates are required.");
        if (fromDate > toDate)
            throw new InvalidOperationException("From date cannot be after to date.");
    }

    private static JournalEntryDto MapJournalToDto(JournalEntry entry) => new()
    {
        Id = entry.Id,
        EntryNumber = entry.EntryNumber,
        EntryDate = entry.EntryDate,
        EntryDateBs = entry.EntryDateBs,
        VoucherType = (VoucherTypeDto)entry.VoucherType,
        ReferenceNumber = entry.ReferenceNumber,
        Narration = entry.Narration,
        TotalDebit = entry.TotalDebit,
        TotalCredit = entry.TotalCredit,
        Status = (TransactionStatusDto)entry.Status,
        ReversedJournalEntryId = entry.ReversedJournalEntryId,
        ReversalReason = entry.ReversalReason,
        Lines = entry.Lines.Select(l => new JournalLineDto
        {
            Id = l.Id,
            LedgerId = l.LedgerId,
            LedgerName = l.Ledger?.Name ?? string.Empty,
            DebitAmount = l.DebitAmount,
            CreditAmount = l.CreditAmount,
            Narration = l.Narration
        }).ToList()
    };
}
