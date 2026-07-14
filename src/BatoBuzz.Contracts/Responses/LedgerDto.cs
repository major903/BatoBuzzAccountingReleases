using BatoBuzz.Contracts.Common;

namespace BatoBuzz.Contracts.Responses;

public class LedgerDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AccountGroupId { get; set; }
    public string Name { get; set; } = null!;
    public string? NameNepali { get; set; }
    public string? Code { get; set; }
    public LedgerTypeDto LedgerType { get; set; }
    public AccountTypeDto AccountType { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; }
}

public class AccountGroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? NameNepali { get; set; }
    public AccountTypeDto AccountType { get; set; }
    public Guid? ParentGroupId { get; set; }
    public int DisplayOrder { get; set; }
}
