namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Represents a user of the accounting system.
/// </summary>
public class User : AuditableEntity
{
    public string UserName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string? FullName { get; private set; }
    public string? Phone { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsLockedOut { get; private set; } = false;
    public DateTime? LockoutEnd { get; private set; }
    public int FailedLoginAttempts { get; private set; } = 0;
    public string PreferredLanguage { get; private set; } = "en";
    public Guid? SelectedCompanyId { get; private set; }
    public Guid? SelectedBranchId { get; private set; }

    // Navigation
    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

    private User() { }

    public static User Create(
        string userName,
        string email,
        string passwordHash,
        string? fullName = null,
        string? phone = null,
        string preferredLanguage = "en")
    {
        return new User
        {
            UserName = userName,
            Email = email,
            PasswordHash = passwordHash,
            FullName = fullName,
            Phone = phone,
            PreferredLanguage = preferredLanguage
        };
    }

    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= 5)
        {
            IsLockedOut = true;
            LockoutEnd = DateTime.UtcNow.AddMinutes(30);
        }
    }

    public void RecordSuccessfulLogin()
    {
        FailedLoginAttempts = 0;
        IsLockedOut = false;
        LockoutEnd = null;
    }

    public void Unlock()
    {
        IsLockedOut = false;
        FailedLoginAttempts = 0;
        LockoutEnd = null;
    }

    public void SelectCompany(Guid companyId, Guid? branchId = null)
    {
        SelectedCompanyId = companyId;
        SelectedBranchId = branchId;
    }

    public void ChangeLanguage(string language)
    {
        PreferredLanguage = language;
    }

    public void ChangePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));

        PasswordHash = passwordHash;
    }

    public void Update(string? fullName = null, string? phone = null, string? email = null)
    {
        if (!string.IsNullOrWhiteSpace(fullName)) FullName = fullName;
        if (phone != null) Phone = phone;
        if (email != null) Email = email;
    }

    public void Deactivate() => IsActive = false;
}
