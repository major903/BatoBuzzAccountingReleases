namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Represents a role that defines a set of permissions.
/// </summary>
public class Role : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; } = false;

    // Navigation
    public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

    private Role() { }

    public static Role Create(string name, string? description = null, bool isSystem = false)
    {
        return new Role
        {
            Name = name,
            Description = description,
            IsSystem = isSystem
        };
    }
}
