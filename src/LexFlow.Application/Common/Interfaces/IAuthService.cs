namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Orchestrates the login/refresh/logout/password-reset/2FA flows described in PRD
/// §17 (API contract) and §20 (security model). Implemented in Infrastructure because
/// it composes IPasswordHasher, IJwtTokenService, ITotpService and IPermissionService —
/// all Infrastructure concerns — plus the persistence needed to record sessions and
/// login history.
/// </summary>
public interface IAuthService
{
    Task<LoginResult> LoginAsync(string tenantSlug, string email, string password, string? ip, string? userAgent, CancellationToken cancellationToken = default);

    Task<LoginResult> CompleteTwoFactorLoginAsync(string pendingToken, string code, string? ip, string? userAgent, CancellationToken cancellationToken = default);

    Task<RefreshResult> RefreshAsync(string refreshToken, string? ip, string? userAgent, CancellationToken cancellationToken = default);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RequestPasswordResetAsync(string tenantSlug, string email, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(string resetToken, string newPassword, CancellationToken cancellationToken = default);

    Task<TwoFaSetupResult> SetupTwoFactorAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    Task ConfirmTwoFactorAsync(Guid userId, Guid tenantId, string code, CancellationToken cancellationToken = default);

    Task<CurrentUserInfo> GetMeAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
}

public enum LoginOutcome
{
    Succeeded,
    RequiresTwoFactor,
    InvalidCredentials,
    AccountLocked,
}

public sealed record LoginResult(
    LoginOutcome Outcome,
    string? AccessToken = null,
    int? ExpiresIn = null,
    string? RefreshToken = null,
    string? PendingToken = null,
    UserSummary? User = null);

public sealed record RefreshResult(string AccessToken, int ExpiresIn, string RefreshToken);

public sealed record UserSummary(Guid Id, string Name, string Role, IReadOnlyCollection<string> Permissions, Guid? BranchId);

public sealed record TwoFaSetupResult(string ProvisioningUri, IReadOnlyList<string> RecoveryCodes);

public sealed record CurrentUserInfo(
    Guid Id,
    string Name,
    string Email,
    string Role,
    Guid? BranchId,
    IReadOnlyCollection<string> Permissions,
    bool TwoFaEnabled);
