using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to portal.portal_messages (lexflow-database Scripts/19_Portal/PortalMessages).
/// Exactly one of SenderClientPortalUserId/SenderStaffUserId is set (DB CHECK backs this),
/// never both. Validation: message length &lt;= 10k chars (enforced by both the DB CHECK and
/// the constructor guard below, same defense-in-depth pattern used throughout this codebase).
/// </summary>
public sealed class PortalMessage : AuditableEntity
{
    private const int MaxBodyLength = 10_000;

    private PortalMessage()
    {
    }

    private PortalMessage(Guid tenantId, Guid threadId, Guid? senderClientPortalUserId, Guid? senderStaffUserId, string body)
    {
        if (body.Length > MaxBodyLength)
        {
            throw new InvalidOperationException($"Message body exceeds the {MaxBodyLength}-character limit.");
        }

        Id = Guid.NewGuid();
        TenantId = tenantId;
        ThreadId = threadId;
        SenderClientPortalUserId = senderClientPortalUserId;
        SenderStaffUserId = senderStaffUserId;
        Body = body;
    }

    public static PortalMessage FromPortalUser(Guid tenantId, Guid threadId, Guid senderClientPortalUserId, string body) =>
        new(tenantId, threadId, senderClientPortalUserId, null, body);

    public static PortalMessage FromStaffUser(Guid tenantId, Guid threadId, Guid senderStaffUserId, string body) =>
        new(tenantId, threadId, null, senderStaffUserId, body);

    public Guid ThreadId { get; private set; }
    public Guid? SenderClientPortalUserId { get; private set; }
    public Guid? SenderStaffUserId { get; private set; }
    public string Body { get; private set; } = null!;
    public DateTimeOffset? ReadAt { get; private set; }

    public void MarkRead() => ReadAt ??= DateTimeOffset.UtcNow;
}
