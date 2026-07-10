using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Management;

/// <summary>Module 14 session manager + login history browser (PRD §17, §20(11)/(12)).</summary>
public sealed class SessionManagementService(LexFlowDbContext db) : ISessionManagementService
{
    public async Task<IReadOnlyList<SessionDto>> GetUserSessionsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var sessions = await db.UserSessions
            .Where(s => s.TenantId == tenantId && s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return sessions.Select(ToDto).ToList();
    }

    public async Task RevokeSessionAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await db.UserSessions.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Id == sessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(UserSession), sessionId);

        session.Revoke();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LoginHistoryDto>> GetLoginHistoryAsync(Guid tenantId, Guid? userId, DateTimeOffset? from, CancellationToken cancellationToken = default)
    {
        var query = db.LoginHistory.Where(h => h.TenantId == tenantId);
        if (userId is { } uid)
        {
            query = query.Where(h => h.UserId == uid);
        }

        if (from is { } fromDate)
        {
            query = query.Where(h => h.At >= fromDate);
        }

        var rows = await query.OrderByDescending(h => h.At).ToListAsync(cancellationToken);
        return rows.Select(h => new LoginHistoryDto(h.Id, h.UserId, h.At, h.Ip, h.Ua, h.Result, h.FailureReason)).ToList();
    }

    private static SessionDto ToDto(UserSession session) => new(
        session.Id,
        session.UserId,
        session.Ua,
        session.Ip,
        session.ExpiresAt,
        session.RevokedAt,
        session.IsActive);
}
