using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Represents a specific permission within the system.
/// </summary>
public class Permission : AuditableEntity
{
    public string Name { get; private set; } = null!; // e.g., "Sales.View"
    public PermissionModule Module { get; private set; }
    public PermissionAction Action { get; private set; }
    public string? Description { get; private set; }

    // Navigation
    public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();

    private Permission() { }

    public static Permission Create(PermissionModule module, PermissionAction action, string? description = null)
    {
        return new Permission
        {
            Name = $"{module}.{action}",
            Module = module,
            Action = action,
            Description = description
        };
    }
}
