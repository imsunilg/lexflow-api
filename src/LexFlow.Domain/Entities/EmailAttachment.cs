using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to comm.email_attachments (lexflow-database
/// Scripts/08_Comm/EmailAttachments). message_sent_at carries the parent
/// comm.email_messages row's partition key, per that table's own FK note.
/// </summary>
public sealed class EmailAttachment : AuditableEntity
{
    private EmailAttachment()
    {
    }

    public EmailAttachment(Guid tenantId, Guid messageId, DateTimeOffset messageSentAt, Guid? documentId, string filename, long? sizeBytes, string? mime)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        MessageId = messageId;
        MessageSentAt = messageSentAt;
        DocumentId = documentId;
        Filename = filename;
        SizeBytes = sizeBytes;
        Mime = mime;
    }

    public Guid MessageId { get; private set; }
    public DateTimeOffset MessageSentAt { get; private set; }
    public Guid? DocumentId { get; private set; }
    public string Filename { get; private set; } = null!;
    public long? SizeBytes { get; private set; }
    public string? Mime { get; private set; }
}
