namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Join entity linking users to roles.
/// </summary>
public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
