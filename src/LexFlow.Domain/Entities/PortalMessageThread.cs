using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to portal.portal_message_threads (lexflow-database Scripts/19_Portal/PortalMessageThreads). Module 17 User Flow #7: one thread per matter, secure messaging with the firm team (never email).</summary>
public sealed class PortalMessageThread : AuditableEntity
{
    private PortalMessageThread()
    {
    }

    public PortalMessageThread(Guid tenantId, Guid matterId, string? subject)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        MatterId = matterId;
        Subject = subject;
    }

    public Guid MatterId { get; private set; }
    public string? Subject { get; private set; }
    public DateTimeOffset? LastMessageAt { get; private set; }

    public void TouchLastMessageAt() => LastMessageAt = DateTimeOffset.UtcNow;
}
