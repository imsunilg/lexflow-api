using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to dms.document_activity (lexflow-database Scripts/05_DMS/DocumentActivity). Module 7 Security Rules: "every view/download/print writes document_activity".</summary>
public sealed class DocumentActivity : AuditableEntity
{
    private DocumentActivity()
    {
    }

    public DocumentActivity(Guid tenantId, Guid documentId, Guid? userId, string action, string? ip, string? ua)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        DocumentId = documentId;
        UserId = userId;
        Action = action;
        At = DateTimeOffset.UtcNow;
        Ip = ip;
        Ua = ua;
    }

    public Guid DocumentId { get; private set; }
    public Guid? UserId { get; private set; }
    public string Action { get; private set; } = null!;
    public DateTimeOffset At { get; private set; }
    public string? Ip { get; private set; }
    public string? Ua { get; private set; }
}
