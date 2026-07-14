namespace BatoBuzz.Domain.Enums;

/// <summary>
/// Classification of ledgers by their purpose.
/// </summary>
public enum LedgerType
{
    General = 1,
    Bank = 2,
    Cash = 3,
    Tax = 4,
    Customer = 5,
    Supplier = 6,
    Inventory = 7,
    Employee = 8
}
