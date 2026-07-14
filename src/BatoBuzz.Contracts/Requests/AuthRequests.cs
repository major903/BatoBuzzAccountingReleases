namespace BatoBuzz.Contracts.Requests;

public class RegisterRequest
{
    public string UserName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? FullName { get; set; }
    public string? Phone { get; set; }
}

public class LoginRequest
{
    public string UserName { get; set; } = null!;
    public string Password { get; set; } = null!;
    public bool RememberMe { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
}

public class CreateUserRequest
{
    public string UserName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public List<Guid> RoleIds { get; set; } = new();
}

public class CreateRoleRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public List<Guid> PermissionIds { get; set; } = new();
}
