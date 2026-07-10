using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to portal.portal_login_history (lexflow-database Scripts/19_Portal/PortalLoginHistory). Backs the portal's own 5-fails/15-min lockout, independent of staff core.login_history.</summary>
public sealed class PortalLoginHistory : AuditableEntity
{
    private PortalLoginHistory()
    {
    }

    public PortalLoginHistory(Guid tenantId, Guid? clientPortalUserId, string result, string? ip, string? ua, string? failureReason = null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ClientPortalUserId = clientPortalUserId;
        At = DateTimeOffset.UtcNow;
        Result = result;
        Ip = ip;
        Ua = ua;
        FailureReason = failureReason;
    }

    public Guid? ClientPortalUserId { get; private set; }
    public DateTimeOffset At { get; private set; }
    public string? Ip { get; private set; }
    public string? Ua { get; private set; }
    public string Result { get; private set; } = null!;
    public string? FailureReason { get; private set; }
}
