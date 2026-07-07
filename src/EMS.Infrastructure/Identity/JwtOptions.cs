namespace EMS.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "EMS";
    public string Audience { get; set; } = "EMS";

    /// <summary>HS256 signing key. Dev value lives in appsettings.Development.json; production reads Azure Key Vault (design §9.1).</summary>
    public string Key { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
