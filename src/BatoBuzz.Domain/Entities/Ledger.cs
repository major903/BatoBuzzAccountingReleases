using BatoBuzz.Domain.Common;
using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Represents a ledger account in the chart of accounts.
/// Ledgers are the leaf-level accounts where transactions are posted.
/// </summary>
public class Ledger : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string? NameNepali { get; private set; }
    public string? Code { get; private set; }
    public LedgerType LedgerType { get; private set; } = LedgerType.General;
    public decimal OpeningBalance { get; private set; } = 0;
    public OpeningBalanceType OpeningBalanceType { get; private set; } = OpeningBalanceType.Debit;
    public decimal CurrentBalance { get; private set; } = 0;
    public bool IsSystem { get; private set; } = false;
    public bool IsActive { get; private set; } = true;

    // For bank/cash ledgers
    public string? BankAccountNumber { get; private set; }
    public string? BankName { get; private set; }
    public string? BankBranch { get; private set; }

    // Navigation
    public Guid AccountGroupId { get; private set; }
    public AccountGroup AccountGroup { get; private set; } = null!;
    public ICollection<JournalLine> JournalLines { get; private set; } = new List<JournalLine>();

    private Ledger() { }

    public static Ledger Create(
        Guid companyId,
        Guid accountGroupId,
        string name,
        LedgerType ledgerType = LedgerType.General,
        string? code = null,
        string? nameNepali = null,
        decimal openingBalance = 0,
        OpeningBalanceType openingBalanceType = OpeningBalanceType.Debit,
        string? bankAccountNumber = null,
        string? bankName = null,
        string? bankBranch = null,
        bool isSystem = false)
    {
        openingBalance = Money.Round(openingBalance);

        return new Ledger
        {
            CompanyId = companyId,
            AccountGroupId = accountGroupId,
            Name = name,
            NameNepali = nameNepali,
            Code = code,
            LedgerType = ledgerType,
            OpeningBalance = openingBalance,
            OpeningBalanceType = openingBalanceType,
            CurrentBalance = openingBalanceType == OpeningBalanceType.Debit ? openingBalance : -openingBalance,
            BankAccountNumber = bankAccountNumber,
            BankName = bankName,
            BankBranch = bankBranch,
            IsSystem = isSystem
        };
    }

    public void UpdateBalance(decimal debitAmount, decimal creditAmount)
    {
        var accountType = AccountGroup.AccountType;
        var isDebitBalance = accountType == AccountType.Asset || accountType == AccountType.Expense || accountType == AccountType.CostOfSales;

        if (isDebitBalance)
            CurrentBalance = Money.Round(CurrentBalance + debitAmount - creditAmount);
        else
            CurrentBalance = Money.Round(CurrentBalance + creditAmount - debitAmount);
    }

    public void Update(string? name = null, string? code = null, bool? isActive = null)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name;
        if (code != null) Code = code;
        if (isActive.HasValue) IsActive = isActive.Value;
    }
}
