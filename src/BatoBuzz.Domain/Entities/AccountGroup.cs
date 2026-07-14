using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Groups related ledgers under a common classification (e.g., Current Assets, Direct Income).
/// </summary>
public class AccountGroup : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string? NameNepali { get; private set; }
    public AccountType AccountType { get; private set; }
    public Guid? ParentGroupId { get; private set; }
    public bool IsSystem { get; private set; } = false;
    public int DisplayOrder { get; private set; }

    // Navigation
    public AccountGroup? ParentGroup { get; private set; }
    public ICollection<AccountGroup> ChildGroups { get; private set; } = new List<AccountGroup>();
    public ICollection<Ledger> Ledgers { get; private set; } = new List<Ledger>();

    private AccountGroup() { }

    public static AccountGroup Create(Guid companyId, string name, AccountType accountType, int displayOrder, string? nameNepali = null, Guid? parentGroupId = null, bool isSystem = false)
    {
        return new AccountGroup
        {
            CompanyId = companyId,
            Name = name,
            NameNepali = nameNepali,
            AccountType = accountType,
            ParentGroupId = parentGroupId,
            IsSystem = isSystem,
            DisplayOrder = displayOrder
        };
    }
}
