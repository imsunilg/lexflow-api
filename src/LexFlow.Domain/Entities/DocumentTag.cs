namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to dms.document_tags (lexflow-database Scripts/05_DMS/DocumentTags). Composite-PK join table — see UserRole for why this does not derive from Entity/AuditableEntity.</summary>
public sealed class DocumentTag
{
    private DocumentTag()
    {
    }

    public DocumentTag(Guid tenantId, Guid documentId, Guid tagId)
    {
        TenantId = tenantId;
        DocumentId = documentId;
        TagId = tagId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid TagId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
}
