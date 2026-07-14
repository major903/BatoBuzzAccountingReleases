namespace BatoBuzz.Contracts.Responses;

/// <summary>
/// Result of an authentication operation.
/// </summary>
public class AuthResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public UserDto? User { get; set; }
    public List<string> Errors { get; set; } = new();

    public static AuthResult Fail(string error) => new()
    {
        Success = false,
        Errors = new List<string> { error }
    };
}

public class UserDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public List<string> Roles { get; set; } = new();
}

public class RoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new();
}
