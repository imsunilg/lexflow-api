using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LexFlow.Infrastructure.Security;

/// <summary>
/// Holds the RS256 signing key used both to issue tokens (Infrastructure) and to
/// validate them (Api's JwtBearer middleware). If no key is configured (PRD §20(3):
/// normally sourced from Key Vault), an ephemeral RSA-2048 key is generated for the
/// process lifetime — safe for local/dev, never for a multi-instance production
/// deployment (tokens wouldn't validate across instances or survive a restart).
/// </summary>
public sealed class JwtSigningKeyProvider
{
    public JwtSigningKeyProvider(IOptions<JwtOptions> options, ILogger<JwtSigningKeyProvider> logger)
    {
        var rsa = RSA.Create();
        var jwtOptions = options.Value;

        if (!string.IsNullOrWhiteSpace(jwtOptions.PrivateKeyPem))
        {
            rsa.ImportFromPem(jwtOptions.PrivateKeyPem);
        }
        else
        {
            logger.LogWarning(
                "Jwt:PrivateKeyPem is not configured — generating an ephemeral RSA-2048 key for this " +
                "process. This is only safe for local development; production must source a stable " +
                "RS256 key pair from Key Vault (PRD §20(3)).");
            rsa.KeySize = 2048;
        }

        var keyId = Guid.NewGuid().ToString("N");
        SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = keyId }, SecurityAlgorithms.RsaSha256);
        ValidationKey = new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = keyId };
    }

    public SigningCredentials SigningCredentials { get; }

    public RsaSecurityKey ValidationKey { get; }
}
