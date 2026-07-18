namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Categorizes inventory items for organization and reporting.
/// </summary>
public class ItemCategory : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public Guid? ParentCategoryId { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Navigation
    public ItemCategory? ParentCategory { get; private set; }
    public ICollection<ItemCategory> ChildCategories { get; private set; } = new List<ItemCategory>();
    public ICollection<Item> Items { get; private set; } = new List<Item>();

    private ItemCategory() { }

    public static ItemCategory Create(Guid companyId, string name, Guid? parentCategoryId = null)
    {
        return new ItemCategory
        {
            CompanyId = companyId,
            Name = name,
            ParentCategoryId = parentCategoryId
        };
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
