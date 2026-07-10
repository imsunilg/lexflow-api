using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to comm.email_threads (lexflow-database Scripts/08_Comm/EmailThreads).
/// MatterId/ClientId both null means the thread is unmatched/ambiguous — AC-CM3's
/// Triage queue is simply "threads in this state," resolved via <see cref="LinkToMatter"/>.
/// </summary>
public sealed class EmailThread : AuditableEntity
{
    private EmailThread()
    {
    }

    public EmailThread(Guid tenantId, string? subject, Guid? matterId, Guid? clientId, Guid? mailboxId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Subject = subject;
        MatterId = matterId;
        ClientId = clientId;
        MailboxId = mailboxId;
    }

    public string? Subject { get; private set; }
    public Guid? MatterId { get; private set; }
    public Guid? ClientId { get; private set; }
    public Guid? MailboxId { get; private set; }
    public DateTimeOffset? LastMessageAt { get; private set; }

    public void LinkToMatter(Guid matterId, Guid? clientId)
    {
        MatterId = matterId;
        ClientId = clientId ?? ClientId;
    }

    public void TouchLastMessageAt(DateTimeOffset sentAt)
    {
        if (LastMessageAt is null || sentAt > LastMessageAt)
        {
            LastMessageAt = sentAt;
        }
    }
}
