namespace BatoBuzz.Contracts.Requests;

public class CreateCompanyRequest
{
    public string Name { get; set; } = null!;
    public string? TradingName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? PanNumber { get; set; }
    public string? VatNumber { get; set; }
    public string? CompanyRegNumber { get; set; }
    public string BaseCurrency { get; set; } = "NPR";
    public DateTime FinancialYearStart { get; set; }
    public DateTime FinancialYearEnd { get; set; }
}

public class CreateBranchRequest
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string? Code { get; set; }
    public string? Address { get; set; }
    public bool IsDefault { get; set; }
}

public class CreateAccountGroupRequest
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string? NameNepali { get; set; }
    public int AccountType { get; set; }
    public Guid? ParentGroupId { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateLedgerRequest
{
    public Guid CompanyId { get; set; }
    public Guid AccountGroupId { get; set; }
    public string Name { get; set; } = null!;
    public string? NameNepali { get; set; }
    public string? Code { get; set; }
    public int LedgerType { get; set; } = 1;
    public decimal OpeningBalance { get; set; }
    public int OpeningBalanceType { get; set; } = 1;
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? BankBranch { get; set; }
}
