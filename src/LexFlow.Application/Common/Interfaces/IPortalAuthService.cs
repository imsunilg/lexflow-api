namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 17: login/refresh/logout/forgot-password orchestration for the portal identity
/// realm — a parallel, independent implementation of IAuthService's shape (not a shared base
/// class) per Module 17 Security: "complete identity separation from staff." Lockout is
/// tracked via portal.portal_login_history, entirely separate from core.login_history.
/// </summary>
public interface IPortalAuthService
{
    Task<PortalLoginResult> LoginAsync(string tenantSlug, string email, string password, string? ip, string? userAgent, CancellationToken cancellationToken = default);

    Task<PortalRefreshResult> RefreshAsync(string refreshToken, string? ip, string? userAgent, CancellationToken cancellationToken = default);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Also the invite-acceptance path: a portal user's first successful reset moves Status from Invited to Active (ClientPortalUser.SetPasswordHash).</summary>
    Task RequestPasswordResetAsync(string tenantSlug, string email, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(string resetToken, string newPassword, CancellationToken cancellationToken = default);
}

public enum PortalLoginOutcome
{
    Succeeded,
    InvalidCredentials,
    AccountLocked,
    PortalDisabled,
}

public sealed record PortalLoginResult(
    PortalLoginOutcome Outcome,
    string? AccessToken = null,
    int? ExpiresIn = null,
    string? RefreshToken = null,
    PortalUserSummary? User = null);

public sealed record PortalRefreshResult(string AccessToken, int ExpiresIn, string RefreshToken);

public sealed record PortalUserSummary(Guid Id, Guid ClientId, string? Name, string Email);
