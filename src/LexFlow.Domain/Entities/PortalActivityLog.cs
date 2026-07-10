namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to portal.portal_activity_log (lexflow-database Scripts/19_Portal/PortalActivityLog).
/// Module 17 Security: "audit all views of documents/invoices." Insert-only, no audit trio of
/// its own — this IS the portal's audit trail, same role as audit.audit_events but scoped to
/// the portal identity realm.
/// </summary>
public sealed class PortalActivityLog
{
    private PortalActivityLog()
    {
    }

    public PortalActivityLog(Guid tenantId, Guid clientPortalUserId, string action, string entityType, Guid? entityId, string? ip, string? ua)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ClientPortalUserId = clientPortalUserId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Ip = ip;
        Ua = ua;
        At = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ClientPortalUserId { get; private set; }
    public string Action { get; private set; } = null!;
    public string EntityType { get; private set; } = null!;
    public Guid? EntityId { get; private set; }
    public string? Ip { get; private set; }
    public string? Ua { get; private set; }
    public DateTimeOffset At { get; private set; }
}
