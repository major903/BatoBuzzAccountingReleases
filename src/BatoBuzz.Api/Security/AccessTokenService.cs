using BatoBuzz.Application.Services;
using BatoBuzz.Domain.Entities;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BatoBuzz.Api.Security;

public interface IAccessTokenValidator
{
    ClaimsPrincipal? Validate(string token);
}

/// <summary>
/// Issues short-lived, HMAC-signed bearer tokens without persisting credentials in
/// the token. The API validates the signature, issuer, audience, and lifetime on
/// every authenticated request.
/// </summary>
public sealed class AccessTokenService : ITokenService, IAccessTokenValidator
{
    private const string TokenVersion = "v2";
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);
    private readonly ApiAuthenticationOptions _options;
    private readonly byte[] _signingKey;

    public AccessTokenService(IOptions<ApiAuthenticationOptions> options)
    {
        _options = options.Value;
        _signingKey = Encoding.UTF8.GetBytes(_options.SigningKey);
    }

    public string GenerateToken(User user) => CreateToken(user);

    public string GenerateOfflineToken(User user) => CreateToken(user);

    public ClaimsPrincipal? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096)
            return null;

        var parts = token.Split('.');
        if (parts.Length != 3 || !string.Equals(parts[0], TokenVersion, StringComparison.Ordinal))
            return null;

        try
        {
            var signedContent = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
            var suppliedSignature = FromBase64Url(parts[2]);
            var expectedSignature = HMACSHA256.HashData(_signingKey, signedContent);
            if (!CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
                return null;

            var payload = JsonSerializer.Deserialize<AccessTokenPayload>(FromBase64Url(parts[1]));
            if (payload is null || payload.Subject == Guid.Empty || string.IsNullOrWhiteSpace(payload.UserName))
                return null;

            var now = DateTimeOffset.UtcNow;
            var issuedAt = DateTimeOffset.FromUnixTimeMilliseconds(payload.IssuedAtUnixMilliseconds);
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAtUnix);
            if (!string.Equals(payload.Issuer, _options.Issuer, StringComparison.Ordinal)
                || !string.Equals(payload.Audience, _options.Audience, StringComparison.Ordinal)
                || issuedAt > now.Add(ClockSkew)
                || expiresAt <= now.Subtract(ClockSkew)
                || expiresAt <= issuedAt)
            {
                return null;
            }

            var subject = payload.Subject.ToString("D");
            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, subject),
                    new Claim("sub", subject),
                    new Claim(ClaimTypes.Name, payload.UserName),
                    new Claim("jti", payload.TokenId),
                    new Claim("iat_ms", payload.IssuedAtUnixMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture))
                },
                SignedBearerAuthenticationHandler.SchemeName,
                ClaimTypes.Name,
                ClaimTypes.Role);

            return new ClaimsPrincipal(identity);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private string CreateToken(User user)
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var payload = new AccessTokenPayload
        {
            Subject = user.Id,
            UserName = user.UserName,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAtUnixMilliseconds = issuedAt.ToUnixTimeMilliseconds(),
            ExpiresAtUnix = issuedAt.AddMinutes(_options.AccessTokenMinutes).ToUnixTimeSeconds(),
            TokenId = Guid.NewGuid().ToString("N")
        };

        var encodedPayload = ToBase64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signedContent = Encoding.ASCII.GetBytes($"{TokenVersion}.{encodedPayload}");
        var signature = HMACSHA256.HashData(_signingKey, signedContent);
        return $"{TokenVersion}.{encodedPayload}.{ToBase64Url(signature)}";
    }

    private static string ToBase64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 += (base64.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            0 => string.Empty,
            _ => throw new FormatException("Invalid Base64Url value.")
        };
        return Convert.FromBase64String(base64);
    }

    private sealed class AccessTokenPayload
    {
        [JsonPropertyName("sub")]
        public Guid Subject { get; init; }

        [JsonPropertyName("name")]
        public string UserName { get; init; } = string.Empty;

        [JsonPropertyName("iss")]
        public string Issuer { get; init; } = string.Empty;

        [JsonPropertyName("aud")]
        public string Audience { get; init; } = string.Empty;

        [JsonPropertyName("iat_ms")]
        public long IssuedAtUnixMilliseconds { get; init; }

        [JsonPropertyName("exp")]
        public long ExpiresAtUnix { get; init; }

        [JsonPropertyName("jti")]
        public string TokenId { get; init; } = string.Empty;
    }
}
