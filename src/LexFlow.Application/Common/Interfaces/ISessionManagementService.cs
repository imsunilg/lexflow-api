namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 14 session manager + login history browser (PRD §17: GET /users/{id}/sessions · DELETE /sessions/{id} · GET /login-history).</summary>
public interface ISessionManagementService
{
    Task<IReadOnlyList<SessionDto>> GetUserSessionsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoginHistoryDto>> GetLoginHistoryAsync(Guid tenantId, Guid? userId, DateTimeOffset? from, CancellationToken cancellationToken = default);
}

public sealed record SessionDto(Guid Id, Guid UserId, string? Ua, string? Ip, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt, bool IsActive);

public sealed record LoginHistoryDto(Guid Id, Guid? UserId, DateTimeOffset At, string? Ip, string? Ua, string Result, string? FailureReason);
