using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to comm.email_messages (lexflow-database
/// Scripts/08_Comm/EmailMessages — PARTITION BY RANGE(sent_at), PK (id, sent_at)).
/// message_id_hdr is the RFC 5322 Message-ID header, relied on for BCC-dropbox
/// dedupe/threading and for email-loop guarding (Module 11 edge case).
/// </summary>
public sealed class EmailMessage : AuditableEntity
{
    private EmailMessage()
    {
    }

    public EmailMessage(Guid tenantId, Guid? threadId, string messageIdHdr, string? inReplyTo, string direction, string? fromAddr, string toAddrsJson, string? subject, string? bodyHtmlSanitized, bool hasAttachments, DateTimeOffset sentAt, Guid? matterId, Guid? clientId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ThreadId = threadId;
        MessageIdHdr = messageIdHdr;
        InReplyTo = inReplyTo;
        Direction = direction;
        FromAddr = fromAddr;
        ToAddrsJson = toAddrsJson;
        Subject = subject;
        BodyHtmlSanitized = bodyHtmlSanitized;
        HasAttachments = hasAttachments;
        SentAt = sentAt;
        MatterId = matterId;
        ClientId = clientId;
    }

    public Guid? ThreadId { get; private set; }
    public string MessageIdHdr { get; private set; } = null!;
    public string? InReplyTo { get; private set; }
    public string Direction { get; private set; } = null!;
    public string? FromAddr { get; private set; }
    public string ToAddrsJson { get; private set; } = "[]";
    public string? Subject { get; private set; }
    public string? BodyHtmlSanitized { get; private set; }
    public bool HasAttachments { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
    public Guid? MatterId { get; private set; }
    public Guid? ClientId { get; private set; }

    public void SetMatterLink(Guid matterId, Guid? clientId)
    {
        MatterId = matterId;
        ClientId = clientId ?? ClientId;
    }
}
