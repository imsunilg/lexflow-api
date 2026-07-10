namespace LexFlow.Infrastructure.Security;

/// <summary>
/// Binds the "Jwt" configuration section. Per PRD §20(3): keys live in Key Vault with
/// 90-day rotation; PrivateKeyPem/PublicKeyPem below are the local representation of
/// whatever DefaultAzureCredential/Key Vault resolves into configuration at startup —
/// this type has no Key Vault–specific code, it just reads whatever IConfiguration hands it.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "https://lexflow.app";

    public string? PrivateKeyPem { get; set; }

    public string? PublicKeyPem { get; set; }

    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    public int RefreshTokenLifetimeDays { get; set; } = 7;
}
