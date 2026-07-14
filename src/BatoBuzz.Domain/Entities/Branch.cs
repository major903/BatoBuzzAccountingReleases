namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Represents a branch or location of a company.
/// </summary>
public class Branch : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string? Code { get; private set; }
    public string? Address { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsDefault { get; private set; } = false;

    private Branch() { }

    public static Branch Create(Guid companyId, string name, string? code = null, string? address = null, bool isDefault = false)
    {
        return new Branch
        {
            CompanyId = companyId,
            Name = name,
            Code = code,
            Address = address,
            IsDefault = isDefault
        };
    }

    public static Branch CreateDefault(Guid companyId, string companyName, string? address = null, string? city = null)
    {
        return Create(companyId, $"{companyName} - Main Office", "MAIN", string.IsNullOrEmpty(city) ? address : $"{address}, {city}", true);
    }

    public void Update(string? name = null, string? code = null, string? address = null)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name;
        if (code != null) Code = code;
        if (address != null) Address = address;
    }

    public void Deactivate() => IsActive = false;
}
