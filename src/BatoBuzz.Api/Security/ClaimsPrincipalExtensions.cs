using System.Security.Claims;

namespace BatoBuzz.Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId) && userId != Guid.Empty
            ? userId
            : throw new InvalidOperationException("The authenticated user identifier is missing or invalid.");
    }
}
