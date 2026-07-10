using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using LexFlow.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LexFlow.Infrastructure.Security;

/// <summary>
/// Issues the JWT RS256 access token + opaque rotating refresh token described in
/// PRD §20(3). The refresh token itself is never persisted — only its SHA-256 hash
/// (core.user_sessions.refresh_hash), so a leaked database backup cannot be replayed.
/// </summary>
public sealed class JwtTokenService(JwtSigningKeyProvider keyProvider, IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessTokenResult IssueAccessToken(
        Guid userId,
        Guid tenantId,
        string role,
        Guid? branchId,
        IReadOnlyCollection<string> permissions,
        string audience = "staff",
        Guid? clientId = null)
    {
        var lifetime = TimeSpan.FromMinutes(_options.AccessTokenLifetimeMinutes);
        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new("tenant", tenantId.ToString()),
            new("role", role),
            // Mirrors the token's own audience so downstream consumers (e.g. the audit
            // interceptor's actor_type, PRD §30) don't need to re-parse validated
            // audience metadata off the principal.
            new("actor_type", audience),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (branchId is { } b)
        {
            claims.Add(new Claim("branch", b.ToString()));
        }

        if (clientId is { } c)
        {
            claims.Add(new Claim("client", c.ToString()));
        }

        claims.AddRange(permissions.Select(p => new Claim("perm", p)));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: keyProvider.SigningCredentials);

        var handler = new JwtSecurityTokenHandler();
        return new AccessTokenResult(handler.WriteToken(token), (int)lifetime.TotalSeconds);
    }

    public (string Token, string Hash) IssueRefreshToken()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes);
        return (token, HashRefreshToken(token));
    }

    public string HashRefreshToken(string refreshToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(refreshToken);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public string IssuePurposeToken(Guid userId, Guid tenantId, string audience, TimeSpan lifetime)
    {
        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("tenant", tenantId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: keyProvider.SigningCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public PurposeTokenPayload? ValidatePurposeToken(string token, string audience)
    {
        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = keyProvider.ValidationKey,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        try
        {
            var principal = handler.ValidateToken(token, validationParameters, out _);
            var userId = Guid.Parse(principal.FindFirst("sub")!.Value);
            var tenantId = Guid.Parse(principal.FindFirst("tenant")!.Value);
            return new PurposeTokenPayload(userId, tenantId);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or FormatException)
        {
            return null;
        }
    }
}
