using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Portal;

/// <summary>
/// Module 17: login/refresh/logout/forgot-password for the portal identity realm — a
/// deliberate parallel to AuthService (staff), not a shared base class, per Module 17
/// Security: "complete identity separation from staff." Lockout: 5 failed attempts / 15 min
/// against portal.portal_login_history (independent of core.login_history).
/// </summary>
public sealed class PortalAuthService(LexFlowDbContext db, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService) : IPortalAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromMinutes(30);
    private const string PasswordResetAudience = "portal-password-reset";
    private const string PortalAudience = "portal";

    public async Task<PortalLoginResult> LoginAsync(string tenantSlug, string email, string password, string? ip, string? userAgent, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Slug == tenantSlug, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            return new PortalLoginResult(PortalLoginOutcome.InvalidCredentials);
        }

        var portalUser = await db.ClientPortalUsers.SingleOrDefaultAsync(u => u.TenantId == tenant.Id && u.Email == email, cancellationToken);

        if (await IsLockedOutAsync(tenant.Id, portalUser?.Id, cancellationToken))
        {
            return new PortalLoginResult(PortalLoginOutcome.AccountLocked);
        }

        if (portalUser is null || portalUser.PasswordHash is null || !passwordHasher.Verify(password, portalUser.PasswordHash))
        {
            await RecordLoginAttemptAsync(tenant.Id, portalUser?.Id, "Failed", ip, userAgent, cancellationToken);
            return new PortalLoginResult(PortalLoginOutcome.InvalidCredentials);
        }

        if (portalUser.Status is "Suspended" or "Deactivated")
        {
            await RecordLoginAttemptAsync(tenant.Id, portalUser.Id, "Failed", ip, userAgent, "PortalDisabled", cancellationToken);
            return new PortalLoginResult(PortalLoginOutcome.PortalDisabled);
        }

        var client = await db.Clients.SingleAsync(c => c.Id == portalUser.ClientId, cancellationToken);
        if (!client.PortalEnabled)
        {
            await RecordLoginAttemptAsync(tenant.Id, portalUser.Id, "Failed", ip, userAgent, "PortalDisabled", cancellationToken);
            return new PortalLoginResult(PortalLoginOutcome.PortalDisabled);
        }

        await RecordLoginAttemptAsync(tenant.Id, portalUser.Id, "Success", ip, userAgent, cancellationToken);
        return await IssueSessionAsync(portalUser, tenant.Id, ip, userAgent, cancellationToken);
    }

    public async Task<PortalRefreshResult> RefreshAsync(string refreshToken, string? ip, string? userAgent, CancellationToken cancellationToken = default)
    {
        var hash = jwtTokenService.HashRefreshToken(refreshToken);
        var session = await db.PortalSessions.SingleOrDefaultAsync(s => s.RefreshHash == hash, cancellationToken);

        if (session is null)
        {
            throw new UnauthorizedAccessException("Refresh token not recognized.");
        }

        if (session.RevokedAt is not null)
        {
            var family = await db.PortalSessions
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

        var portalUser = await db.ClientPortalUsers.SingleOrDefaultAsync(u => u.Id == session.ClientPortalUserId && u.TenantId == session.TenantId, cancellationToken);
        if (portalUser is null || portalUser.Status is "Suspended" or "Deactivated")
        {
            session.Revoke();
            await db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Portal user is no longer active.");
        }

        session.Revoke();

        var (newRefreshToken, newHash) = jwtTokenService.IssueRefreshToken();
        var newSession = new PortalSession(
            session.TenantId,
            portalUser.Id,
            newHash,
            session.FamilyId,
            DateTimeOffset.UtcNow.Add(RefreshTokenLifetime),
            userAgent,
            ip);
        await db.PortalSessions.AddAsync(newSession, cancellationToken);

        var accessToken = jwtTokenService.IssueAccessToken(portalUser.Id, portalUser.TenantId, "PortalClient", null, [], PortalAudience, portalUser.ClientId);

        await db.SaveChangesAsync(cancellationToken);

        return new PortalRefreshResult(accessToken.Token, accessToken.ExpiresInSeconds, newRefreshToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = jwtTokenService.HashRefreshToken(refreshToken);
        var session = await db.PortalSessions.SingleOrDefaultAsync(s => s.RefreshHash == hash, cancellationToken);
        if (session is null || session.RevokedAt is not null)
        {
            return;
        }

        session.Revoke();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RequestPasswordResetAsync(string tenantSlug, string email, CancellationToken cancellationToken = default)
    {
        // Uniform no-op on unknown tenant/email — prevents account enumeration (PRD §20(15)),
        // same posture as AuthService.RequestPasswordResetAsync. Actual token delivery is a
        // Communication-module concern, out of scope here.
        var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Slug == tenantSlug, cancellationToken);
        if (tenant is null)
        {
            return;
        }

        var portalUser = await db.ClientPortalUsers.SingleOrDefaultAsync(u => u.TenantId == tenant.Id && u.Email == email, cancellationToken);
        if (portalUser is null)
        {
            return;
        }

        jwtTokenService.IssuePurposeToken(portalUser.Id, tenant.Id, PasswordResetAudience, PasswordResetLifetime);
    }

    public async Task ResetPasswordAsync(string resetToken, string newPassword, CancellationToken cancellationToken = default)
    {
        var payload = jwtTokenService.ValidatePurposeToken(resetToken, PasswordResetAudience)
            ?? throw new UnauthorizedAccessException("Reset token is invalid or expired.");

        var portalUser = await db.ClientPortalUsers.SingleOrDefaultAsync(u => u.Id == payload.UserId && u.TenantId == payload.TenantId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Reset token is invalid or expired.");

        // Also the invite-acceptance path: SetPasswordHash moves Status Invited -> Active.
        portalUser.SetPasswordHash(passwordHasher.Hash(newPassword));

        var activeSessions = await db.PortalSessions
            .Where(s => s.ClientPortalUserId == portalUser.Id && s.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in activeSessions)
        {
            session.Revoke();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<PortalLoginResult> IssueSessionAsync(ClientPortalUser portalUser, Guid tenantId, string? ip, string? userAgent, CancellationToken cancellationToken)
    {
        var accessToken = jwtTokenService.IssueAccessToken(portalUser.Id, tenantId, "PortalClient", null, [], PortalAudience, portalUser.ClientId);
        var (refreshToken, refreshHash) = jwtTokenService.IssueRefreshToken();

        var session = new PortalSession(
            tenantId,
            portalUser.Id,
            refreshHash,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.Add(RefreshTokenLifetime),
            userAgent,
            ip);
        await db.PortalSessions.AddAsync(session, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var summary = new PortalUserSummary(portalUser.Id, portalUser.ClientId, portalUser.Name, portalUser.Email);
        return new PortalLoginResult(PortalLoginOutcome.Succeeded, accessToken.Token, accessToken.ExpiresInSeconds, refreshToken, summary);
    }

    private async Task<bool> IsLockedOutAsync(Guid tenantId, Guid? portalUserId, CancellationToken cancellationToken)
    {
        if (portalUserId is null)
        {
            return false;
        }

        var since = DateTimeOffset.UtcNow.Subtract(LockoutWindow);
        var recentFailures = await db.PortalLoginHistory
            .Where(h => h.TenantId == tenantId && h.ClientPortalUserId == portalUserId && h.Result == "Failed" && h.At >= since)
            .CountAsync(cancellationToken);

        return recentFailures >= MaxFailedAttempts;
    }

    private Task RecordLoginAttemptAsync(Guid tenantId, Guid? portalUserId, string result, string? ip, string? userAgent, CancellationToken cancellationToken)
        => RecordLoginAttemptAsync(tenantId, portalUserId, result, ip, userAgent, null, cancellationToken);

    private async Task RecordLoginAttemptAsync(Guid tenantId, Guid? portalUserId, string result, string? ip, string? userAgent, string? failureReason, CancellationToken cancellationToken)
    {
        var entry = new PortalLoginHistory(tenantId, portalUserId, result, ip, userAgent, failureReason);
        await db.PortalLoginHistory.AddAsync(entry, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
