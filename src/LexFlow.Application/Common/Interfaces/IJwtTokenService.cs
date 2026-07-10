namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Issues/validates the access + refresh token pair described in PRD §20(3):
/// JWT RS256 access token (15 min); refresh token (7 days, rotating, family-based
/// reuse detection). Claims: sub, tenant, role, branch, jti.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// <paramref name="clientId"/> is set only for portal-audience tokens (a "client" claim,
    /// PRD Module 17 Security: "client_id from token, never from payload") — every portal
    /// service method derives its ownership scope from this claim exclusively.
    /// </summary>
    AccessTokenResult IssueAccessToken(
        Guid userId,
        Guid tenantId,
        string role,
        Guid? branchId,
        IReadOnlyCollection<string> permissions,
        string audience = "staff",
        Guid? clientId = null);

    /// <summary>Generates the opaque refresh-token value; the caller persists only its hash (Sessions.refresh_hash).</summary>
    (string Token, string Hash) IssueRefreshToken();

    string HashRefreshToken(string refreshToken);

    /// <summary>
    /// Issues a narrow-purpose JWT (audience-bound, e.g. "2fa-pending" or "password-reset")
    /// carrying only sub/tenant — used for the short-lived intermediate steps between
    /// login and 2FA verification, and for password-reset links (PRD §17, §20(3)).
    /// </summary>
    string IssuePurposeToken(Guid userId, Guid tenantId, string audience, TimeSpan lifetime);

    PurposeTokenPayload? ValidatePurposeToken(string token, string audience);
}

public sealed record AccessTokenResult(string Token, int ExpiresInSeconds);

public sealed record PurposeTokenPayload(Guid UserId, Guid TenantId);
