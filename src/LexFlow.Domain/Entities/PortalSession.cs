using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to portal.portal_sessions (lexflow-database Scripts/19_Portal/PortalSessions).
/// Portal refresh-token rotation with family-based reuse detection — same shape/purpose as
/// UserSession, kept as a fully separate table (Module 17 Security: "complete identity
/// separation from staff").
/// </summary>
public sealed class PortalSession : AuditableEntity
{
    private PortalSession()
    {
    }

    public PortalSession(Guid tenantId, Guid clientPortalUserId, string refreshHash, Guid familyId, DateTimeOffset expiresAt, string? ua = null, string? ip = null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ClientPortalUserId = clientPortalUserId;
        RefreshHash = refreshHash;
        FamilyId = familyId;
        ExpiresAt = expiresAt;
        Ua = ua;
        Ip = ip;
    }

    public Guid ClientPortalUserId { get; private set; }
    public string RefreshHash { get; private set; } = null!;
    public Guid FamilyId { get; private set; }
    public string? Ua { get; private set; }
    public string? Ip { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    public void Revoke() => RevokedAt = DateTimeOffset.UtcNow;
}
