namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Represents a storage location for inventory.
/// </summary>
public class Warehouse : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string? Code { get; private set; }
    public string? Address { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsDefault { get; private set; } = false;

    // Navigation
    public ICollection<StockBalance> StockBalances { get; private set; } = new List<StockBalance>();

    private Warehouse() { }

    public static Warehouse Create(Guid companyId, string name, string? code = null, string? address = null, bool isDefault = false)
    {
        return new Warehouse
        {
            CompanyId = companyId,
            Name = name,
            Code = code,
            Address = address,
            IsDefault = isDefault
        };
    }
}
