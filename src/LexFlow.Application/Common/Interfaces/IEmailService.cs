namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 11 email — thread/message queries, compose+send (delegates the actual send to
/// the tenant's connected IEmailSync provider), thread-matter linking, and the BCC-dropbox
/// inbound handler (AC-CM3: matched by sender email -> client; ambiguous/unmatched goes to
/// the Triage queue, which is simply "threads with no matter/client link yet").
/// </summary>
public interface IEmailService : IInboundEmailHandler
{
    Task<EmailThreadDto> SendAsync(Guid tenantId, Guid? actorId, Guid mailboxId, EmailSendRequest request, Guid? matterId, Guid? clientId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailThreadDto>> GetThreadsAsync(Guid tenantId, Guid? matterId, Guid? clientId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailThreadDto>> GetTriageQueueAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailMessageDto>> GetMessagesAsync(Guid tenantId, Guid threadId, CancellationToken cancellationToken = default);

    /// <summary>Resolves an ambiguous/unmatched thread — "user confirms ambiguous" (Module 11 user flow).</summary>
    Task<EmailThreadDto> LinkThreadToMatterAsync(Guid tenantId, Guid threadId, Guid matterId, Guid? clientId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Split out of <see cref="IEmailService"/> so the per-provider <c>IEmailSync</c>
/// implementations (GmailSyncService, MicrosoftGraphEmailSyncService) can depend on just the
/// inbound-handling slice they actually call from PullChangesAsync, instead of the full
/// IEmailService — which itself depends on <c>IEnumerable&lt;IEmailSync&gt;</c> for SendAsync's
/// provider lookup. Depending on IEmailService directly from an IEmailSync implementation is a
/// circular DI graph (IEmailSync -> IEmailService -> IEnumerable&lt;IEmailSync&gt;) that the
/// container refuses to build.
/// </summary>
public interface IInboundEmailHandler
{
    /// <summary>
    /// The `{tenant-key}@in.lexflow.app` BCC-dropbox address handler: matches the sender
    /// (and any other recipient) email against crm.clients/crm.client_contacts to an
    /// existing client; if exactly one client (and a single open matter) matches, files
    /// directly; if zero or multiple candidates match, the thread is created with no
    /// matter/client link (i.e. lands in the Triage queue) per AC-CM3.
    /// </summary>
    Task<EmailThreadDto> HandleInboundAsync(Guid tenantId, InboundEmailInput input, CancellationToken cancellationToken = default);
}

public sealed record InboundEmailInput(string MessageIdHdr, string? InReplyTo, string FromAddr, IReadOnlyList<string> ToAddrs, string? Subject, string BodyHtml, DateTimeOffset SentAt, IReadOnlyList<EmailAttachmentInput>? Attachments);

public sealed record EmailThreadDto(Guid Id, string? Subject, Guid? MatterId, Guid? ClientId, Guid? MailboxId, DateTimeOffset? LastMessageAt);

public sealed record EmailMessageDto(Guid Id, Guid? ThreadId, string MessageIdHdr, string Direction, string? FromAddr, IReadOnlyList<string> ToAddrs, string? Subject, string? BodyHtmlSanitized, bool HasAttachments, DateTimeOffset SentAt);
