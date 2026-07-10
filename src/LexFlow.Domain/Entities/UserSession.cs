using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to core.user_sessions (lexflow-database Scripts/02_Core/Sessions).
/// Backs JWT refresh-token rotation with family-based reuse detection (PRD §20(3)).
/// </summary>
public sealed class UserSession : AuditableEntity
{
    private UserSession()
    {
    }

    public UserSession(Guid tenantId, Guid userId, string refreshHash, Guid familyId, DateTimeOffset expiresAt, string? ua = null, string? ip = null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        UserId = userId;
        RefreshHash = refreshHash;
        FamilyId = familyId;
        ExpiresAt = expiresAt;
        Ua = ua;
        Ip = ip;
    }

    public Guid UserId { get; private set; }
    public string RefreshHash { get; private set; } = null!;
    public Guid FamilyId { get; private set; }
    public string? Ua { get; private set; }
    public string? Ip { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    public void Revoke() => RevokedAt = DateTimeOffset.UtcNow;
}
