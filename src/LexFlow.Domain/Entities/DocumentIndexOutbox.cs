using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to dms.document_index_outbox (lexflow-database
/// Scripts/05_DMS/DocumentIndexOutbox, additive migration). Written in the SAME
/// SaveChanges transaction as the document/version insert (transactional-outbox
/// pattern) so an Elasticsearch index write can never be lost even if the
/// dispatcher/indexer worker is temporarily down — see DocumentIndexDispatchService.
/// </summary>
public sealed class DocumentIndexOutbox : AuditableEntity
{
    private DocumentIndexOutbox()
    {
    }

    public DocumentIndexOutbox(Guid tenantId, Guid documentId, Guid? versionId, string operation)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        DocumentId = documentId;
        VersionId = versionId;
        Operation = operation;
        Status = "Pending";
    }

    public Guid DocumentId { get; private set; }
    public Guid? VersionId { get; private set; }
    public string Operation { get; private set; } = null!;
    public string Status { get; private set; } = "Pending";
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? DispatchedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    public void MarkDispatched() => (Status, DispatchedAt) = ("Dispatched", DateTimeOffset.UtcNow);

    public void MarkDone() => (Status, ProcessedAt) = ("Done", DateTimeOffset.UtcNow);

    public void MarkFailed(string error)
    {
        Status = "Failed";
        Attempts++;
        LastError = error;
    }
}
