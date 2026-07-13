using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Contracts.Responses;
using BatoBuzz.Domain.Entities;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace BatoBuzz.Application.Services;

public class AuthService : IAuthService
{
    private const int PasswordSaltSize = 16;
    private const int PasswordKeySize = 32;
    private const int PasswordIterations = 210_000;
    private const int MaximumPasswordLength = 128;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;

    public AuthService(IUnitOfWork unitOfWork, ITokenService tokenService)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        if (request is null)
            return AuthResult.Fail("Registration details are required.");

        var userName = request.UserName?.Trim().ToLowerInvariant() ?? string.Empty;
        if (userName.Length is < 3 or > 100)
            return AuthResult.Fail("Username must be between 3 and 100 characters.");
        if (userName.Any(character =>
                !(char.IsLetterOrDigit(character) || character is '.' or '_' or '-')))
        {
            return AuthResult.Fail(
                "Username can contain only letters, numbers, dots, underscores, and hyphens.");
        }

        var password = request.Password ?? string.Empty;
        if (password.Length is < 8 or > MaximumPasswordLength)
            return AuthResult.Fail($"Password must be between 8 and {MaximumPasswordLength} characters.");

        var email = string.IsNullOrWhiteSpace(request.Email)
            ? $"{userName.ToLowerInvariant()}@local.batobuzz"
            : request.Email.Trim().ToLowerInvariant();
        if (email.Length > 200
            || !MailAddress.TryCreate(email, out var parsedEmail)
            || !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            return AuthResult.Fail("A valid email address is required.");
        }

        var fullName = string.IsNullOrWhiteSpace(request.FullName)
            ? null
            : request.FullName.Trim();
        var phone = string.IsNullOrWhiteSpace(request.Phone)
            ? null
            : request.Phone.Trim();
        if (fullName?.Length > 200)
            return AuthResult.Fail("Full name cannot exceed 200 characters.");
        if (phone?.Length > 50)
            return AuthResult.Fail("Phone cannot exceed 50 characters.");

        if (await _unitOfWork.Users.ExistsByUserNameAsync(userName))
            return AuthResult.Fail("Username already exists.");
        if (await _unitOfWork.Users.GetByEmailAsync(email) is not null)
            return AuthResult.Fail("Email address already exists.");

        var user = User.Create(
            userName,
            email,
            HashPassword(password),
            fullName,
            phone);

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return new AuthResult
        {
            Success = true,
            Token = _tokenService.GenerateToken(user),
            User = MapToDto(user)
        };
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        var userName = request?.UserName?.Trim() ?? string.Empty;
        var password = request?.Password ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userName)
            || string.IsNullOrEmpty(password)
            || password.Length > MaximumPasswordLength)
        {
            return AuthResult.Fail("Invalid username or password.");
        }

        var user = await _unitOfWork.Users.GetByUserNameAsync(userName);
        if (user == null)
            return AuthResult.Fail("Invalid username or password.");

        if (!user.IsActive)
            return AuthResult.Fail("Account is deactivated.");

        if (user.IsLockedOut && user.LockoutEnd > DateTime.UtcNow)
            return AuthResult.Fail($"Account is locked. Try again after {user.LockoutEnd:O}.");

        if (!VerifyPassword(password, user.PasswordHash))
        {
            user.RecordFailedLogin();
            await _unitOfWork.SaveChangesAsync();
            return AuthResult.Fail("Invalid username or password.");
        }

        if (NeedsPasswordRehash(user.PasswordHash))
            user.ChangePasswordHash(HashPassword(password));

        user.RecordSuccessfulLogin();
        await _unitOfWork.SaveChangesAsync();

        return new AuthResult
        {
            Success = true,
            Token = _tokenService.GenerateToken(user),
            User = MapToDto(user)
        };
    }

    public Task<AuthResult> LoginOfflineAsync(LoginRequest request) => LoginAsync(request);

    public async Task LogoutAsync(Guid userId)
    {
        if (userId == Guid.Empty)
            return;

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            return;

        user.SetModifiedBy(userId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        if (userId == Guid.Empty || request is null)
            return false;

        var currentPassword = request.CurrentPassword ?? string.Empty;
        var newPassword = request.NewPassword ?? string.Empty;
        if (string.IsNullOrEmpty(currentPassword)
            || currentPassword.Length > MaximumPasswordLength
            || newPassword.Length is < 8 or > MaximumPasswordLength)
        {
            return false;
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || !VerifyPassword(currentPassword, user.PasswordHash))
            return false;

        user.ChangePasswordHash(HashPassword(newPassword));
        user.SetModifiedBy(userId);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(PasswordSaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            PasswordKeySize);

        return "pbkdf2-sha256$" + PasswordIterations + "$"
            + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(key);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        try
        {
            if (hash.StartsWith("pbkdf2-sha256$", StringComparison.Ordinal))
            {
                var parts = hash.Split('$');
                if (parts.Length != 4
                    || !int.TryParse(parts[1], out var iterations)
                    || iterations is < 10_000 or > 2_000_000)
                {
                    return false;
                }

                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                if (salt.Length < 8 || expected.Length < 16)
                    return false;

                var actual = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expected.Length);

                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }

            var legacyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            var legacyHash = Convert.ToHexString(legacyBytes);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(legacyHash),
                Encoding.UTF8.GetBytes(hash));
        }
        catch (Exception exception) when (
            exception is FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    private static bool NeedsPasswordRehash(string hash) =>
        !hash.StartsWith(
            "pbkdf2-sha256$" + PasswordIterations + "$",
            StringComparison.Ordinal);

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        Email = user.Email,
        FullName = user.FullName,
        Phone = user.Phone,
        IsActive = user.IsActive,
        PreferredLanguage = user.PreferredLanguage,
        Roles = user.UserRoles.Select(userRole => userRole.Role.Name).ToList()
    };
}

public interface ITokenService
{
    string GenerateToken(User user);
    string GenerateOfflineToken(User user);
}

public sealed class LocalTokenService : ITokenService
{
    public string GenerateToken(User user) => GenerateOfflineToken(user);

    public string GenerateOfflineToken(User user)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{user.Id:N}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:{Convert.ToBase64String(bytes)}"));
    }
}
