using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Domain.Enums;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Security;

/// <summary>
/// Implements the login/refresh/logout/reset/2FA orchestration for PRD §17 + §20.
/// Lockout: 5 failed attempts / 15 min (§20(2)). Refresh rotation: each use retires
/// the presented session row and mints a new one sharing the same family_id; a
/// retired row's hash is left untouched so a replay of an already-rotated token is
/// recognized and revokes the whole family (§20(3): "token theft → revoke family + alert").
/// </summary>
public sealed class AuthService(
    LexFlowDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    ITotpService totpService,
    IPermissionService permissionService) : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan TwoFaPendingLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromMinutes(30);
    private const string TwoFaPendingAudience = "2fa-pending";
    private const string PasswordResetAudience = "password-reset";

    public async Task<LoginResult> LoginAsync(string tenantSlug, string email, string password, string? ip, string? userAgent, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Slug == tenantSlug, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            return new LoginResult(LoginOutcome.InvalidCredentials);
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.TenantId == tenant.Id && u.Email == email, cancellationToken);

        if (await IsLockedOutAsync(tenant.Id, user?.Id, cancellationToken))
        {
            return new LoginResult(LoginOutcome.AccountLocked);
        }

        if (user is null || user.PasswordHash is null || !passwordHasher.Verify(password, user.PasswordHash) || user.Status != UserStatus.Active)
        {
            await RecordLoginAttemptAsync(tenant.Id, user?.Id, "Failed", ip, userAgent, cancellationToken);
            return new LoginResult(LoginOutcome.InvalidCredentials);
        }

        if (user.TwoFaEnabled)
        {
            await RecordLoginAttemptAsync(tenant.Id, user.Id, "TwoFaRequired", ip, userAgent, cancellationToken);
            var pendingToken = jwtTokenService.IssuePurposeToken(user.Id, tenant.Id, TwoFaPendingAudience, TwoFaPendingLifetime);
            return new LoginResult(LoginOutcome.RequiresTwoFactor, PendingToken: pendingToken);
        }

        await RecordLoginAttemptAsync(tenant.Id, user.Id, "Success", ip, userAgent, cancellationToken);
        return await IssueSessionAsync(user, tenant.Id, ip, userAgent, cancellationToken);
    }

    public async Task<LoginResult> CompleteTwoFactorLoginAsync(string pendingToken, string code, string? ip, string? userAgent, CancellationToken cancellationToken = default)
    {
        var payload = jwtTokenService.ValidatePurposeToken(pendingToken, TwoFaPendingAudience);
        if (payload is null)
        {
            return new LoginResult(LoginOutcome.InvalidCredentials);
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == payload.UserId && u.TenantId == payload.TenantId, cancellationToken);
        if (user is null || !user.TwoFaEnabled || user.TwoFaSecret is null || !totpService.ValidateCode(user.TwoFaSecret, code))
        {
            await RecordLoginAttemptAsync(payload.TenantId, payload.UserId, "Failed", ip, userAgent, cancellationToken);
            return new LoginResult(LoginOutcome.InvalidCredentials);
        }

        await RecordLoginAttemptAsync(payload.TenantId, user.Id, "Success", ip, userAgent, cancellationToken);
        return await IssueSessionAsync(user, payload.TenantId, ip, userAgent, cancellationToken);
    }

    public async Task<RefreshResult> RefreshAsync(string refreshToken, string? ip, string? userAgent, CancellationToken cancellationToken = default)
    {
        var hash = jwtTokenService.HashRefreshToken(refreshToken);
        var session = await db.UserSessions.SingleOrDefaultAsync(s => s.RefreshHash == hash, cancellationToken);

        if (session is null)
        {
            throw new UnauthorizedAccessException("Refresh token not recognized.");
        }

        if (session.RevokedAt is not null)
        {
            // Reuse of an already-rotated token — presumed theft. Revoke the whole family.
            var family = await db.UserSessions
                .Where(s => s.FamilyId == session.FamilyId && s.RevokedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var s in family)
            {
                s.Revoke();
            }

            await db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Refresh token reuse detected; session family revoked.");
        }

        if (!session.IsActive)
        {
            throw new UnauthorizedAccessException("Refresh token expired.");
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == session.UserId && u.TenantId == session.TenantId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            session.Revoke();
            await db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("User is no longer active.");
        }

        session.Revoke();

        var (newRefreshToken, newHash) = jwtTokenService.IssueRefreshToken();
        var newSession = new UserSession(
            session.TenantId,
            user.Id,
            newHash,
            session.FamilyId,
            DateTimeOffset.UtcNow.Add(RefreshTokenLifetime),
            userAgent,
            ip);
        await db.UserSessions.AddAsync(newSession, cancellationToken);

        var (role, permissions, branchId) = await ResolveAccessAsync(user, cancellationToken);
        var accessToken = jwtTokenService.IssueAccessToken(user.Id, user.TenantId, role, branchId, permissions);

        await db.SaveChangesAsync(cancellationToken);

        return new RefreshResult(accessToken.Token, accessToken.ExpiresInSeconds, newRefreshToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = jwtTokenService.HashRefreshToken(refreshToken);
        var session = await db.UserSessions.SingleOrDefaultAsync(s => s.RefreshHash == hash, cancellationToken);
        if (session is null || session.RevokedAt is not null)
        {
            return;
        }

        session.Revoke();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RequestPasswordResetAsync(string tenantSlug, string email, CancellationToken cancellationToken = default)
    {
        // Always no-op silently on unknown tenant/email — uniform response prevents
        // account enumeration (PRD §20(15)). The reset token/link delivery itself is a
        // Communication-module (comm) concern, out of scope for this Module 14 prompt.
        var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Slug == tenantSlug, cancellationToken);
        if (tenant is null)
        {
            return;
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.TenantId == tenant.Id && u.Email == email, cancellationToken);
        if (user is null)
        {
            return;
        }

        jwtTokenService.IssuePurposeToken(user.Id, tenant.Id, PasswordResetAudience, PasswordResetLifetime);
    }

    public async Task ResetPasswordAsync(string resetToken, string newPassword, CancellationToken cancellationToken = default)
    {
        var payload = jwtTokenService.ValidatePurposeToken(resetToken, PasswordResetAudience)
            ?? throw new UnauthorizedAccessException("Reset token is invalid or expired.");

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == payload.UserId && u.TenantId == payload.TenantId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Reset token is invalid or expired.");

        user.SetPasswordHash(passwordHasher.Hash(newPassword));

        var activeSessions = await db.UserSessions
            .Where(s => s.UserId == user.Id && s.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in activeSessions)
        {
            session.Revoke();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TwoFaSetupResult> SetupTwoFactorAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken)
            ?? throw new UnauthorizedAccessException("User not found.");

        var secret = totpService.GenerateSecret();
        user.SetPendingTwoFactorSecret(secret);
        await db.SaveChangesAsync(cancellationToken);

        var provisioningUri = totpService.BuildProvisioningUri(secret, user.Email);
        var recoveryCodes = totpService.GenerateRecoveryCodes();
        return new TwoFaSetupResult(provisioningUri, recoveryCodes);
    }

    public async Task ConfirmTwoFactorAsync(Guid userId, Guid tenantId, string code, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken)
            ?? throw new UnauthorizedAccessException("User not found.");

        if (user.TwoFaSecret is null || !totpService.ValidateCode(user.TwoFaSecret, code))
        {
            throw new UnauthorizedAccessException("Invalid 2FA code.");
        }

        user.ConfirmTwoFactor();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CurrentUserInfo> GetMeAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken)
            ?? throw new UnauthorizedAccessException("User not found.");

        var (role, permissions, branchId) = await ResolveAccessAsync(user, cancellationToken);
        return new CurrentUserInfo(user.Id, user.Name, user.Email, role, branchId, permissions, user.TwoFaEnabled);
    }

    private async Task<LoginResult> IssueSessionAsync(User user, Guid tenantId, string? ip, string? userAgent, CancellationToken cancellationToken)
    {
        var (role, permissions, branchId) = await ResolveAccessAsync(user, cancellationToken);
        var accessToken = jwtTokenService.IssueAccessToken(user.Id, tenantId, role, branchId, permissions);
        var (refreshToken, refreshHash) = jwtTokenService.IssueRefreshToken();

        var session = new UserSession(
            tenantId,
            user.Id,
            refreshHash,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.Add(RefreshTokenLifetime),
            userAgent,
            ip);
        await db.UserSessions.AddAsync(session, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var summary = new UserSummary(user.Id, user.Name, role, permissions, branchId);
        return new LoginResult(LoginOutcome.Succeeded, accessToken.Token, accessToken.ExpiresInSeconds, refreshToken, User: summary);
    }

    private async Task<(string Role, IReadOnlyCollection<string> Permissions, Guid? BranchId)> ResolveAccessAsync(User user, CancellationToken cancellationToken)
    {
        var primaryRole = await (
            from userRole in db.UserRoles
            join role in db.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == user.Id && userRole.TenantId == user.TenantId
            select role.Key).FirstOrDefaultAsync(cancellationToken) ?? "none";

        var effectivePermissions = await permissionService.GetEffectivePermissionsAsync(user.Id, user.TenantId, cancellationToken);
        var permissionKeys = effectivePermissions.Select(p => p.Key).ToArray();

        return (primaryRole, permissionKeys, user.BranchId);
    }

    private async Task<bool> IsLockedOutAsync(Guid tenantId, Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is null)
        {
            return false;
        }

        var since = DateTimeOffset.UtcNow.Subtract(LockoutWindow);
        var recentFailures = await db.LoginHistory
            .Where(h => h.TenantId == tenantId && h.UserId == userId && h.Result == "Failed" && h.At >= since)
            .CountAsync(cancellationToken);

        return recentFailures >= MaxFailedAttempts;
    }

    private async Task RecordLoginAttemptAsync(Guid tenantId, Guid? userId, string result, string? ip, string? userAgent, CancellationToken cancellationToken)
    {
        var entry = new LoginHistory(tenantId, userId, result, ip, userAgent);
        await db.LoginHistory.AddAsync(entry, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
