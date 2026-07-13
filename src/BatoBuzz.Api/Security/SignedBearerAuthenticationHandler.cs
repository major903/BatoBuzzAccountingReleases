using BatoBuzz.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Encodings.Web;

namespace BatoBuzz.Api.Security;

public sealed class SignedBearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Bearer";
    private readonly IAccessTokenValidator _tokenValidator;
    private readonly BatoBuzzDbContext _dbContext;

    public SignedBearerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IAccessTokenValidator tokenValidator,
        BatoBuzzDbContext dbContext)
        : base(options, logger, encoder)
    {
        _tokenValidator = tokenValidator;
        _dbContext = dbContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
            return AuthenticateResult.NoResult();

        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.Fail("The Authorization header must use the Bearer scheme.");

        var token = authorization[prefix.Length..].Trim();
        var principal = _tokenValidator.Validate(token);
        if (principal is null)
            return AuthenticateResult.Fail("The access token is invalid or expired.");

        var issuedAtValue = principal.FindFirst("iat_ms")?.Value;
        if (!long.TryParse(issuedAtValue, NumberStyles.None, CultureInfo.InvariantCulture, out var issuedAtMilliseconds))
            return AuthenticateResult.Fail("The access token has no valid issue time.");

        var userId = principal.GetRequiredUserId();
        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new { candidate.IsActive, candidate.ModifiedAt })
            .SingleOrDefaultAsync(Context.RequestAborted);
        if (user is null || !user.IsActive)
            return AuthenticateResult.Fail("The user account is unavailable or inactive.");

        if (user.ModifiedAt.HasValue)
        {
            var validAfter = DateTime.SpecifyKind(user.ModifiedAt.Value, DateTimeKind.Utc);
            var issuedAt = DateTimeOffset.FromUnixTimeMilliseconds(issuedAtMilliseconds).UtcDateTime;
            if (issuedAt <= validAfter)
                return AuthenticateResult.Fail("The access token was revoked.");
        }

        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
