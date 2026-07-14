namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Unit of measurement for inventory items (e.g., Piece, Kg, Liter, Box).
/// </summary>
public class Unit : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string? ShortName { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Navigation
    public ICollection<Item> Items { get; private set; } = new List<Item>();

    private Unit() { }

    public static Unit Create(Guid companyId, string name, string? shortName = null)
    {
        return new Unit
        {
            CompanyId = companyId,
            Name = name,
            ShortName = shortName ?? name
        };
    }
}
