namespace BatoBuzz.Api.Security;

public sealed class ApiAuthenticationOptions
{
    public const string SectionName = "Authentication";

    public string Issuer { get; set; } = "BatoBuzz.Api";
    public string Audience { get; set; } = "BatoBuzz.Clients";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
}
