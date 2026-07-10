namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 11 mailbox sync — one implementation per provider (Gmail, Microsoft Graph),
/// resolved by <see cref="Provider"/> name, mirroring the ICalendarSync/ISignatureProvider
/// pattern used elsewhere in this codebase. AC-CM1: a send must appear in the provider's
/// own Sent folder (not just LexFlow's DB) within 30s, hence <see cref="SendAsync"/>
/// going through the provider API rather than raw SMTP.
/// </summary>
public interface IEmailSync
{
    string Provider { get; }

    string GetAuthorizationUrl(Guid tenantId, Guid userId, string redirectUri);

    Task<Guid> HandleOAuthCallbackAsync(Guid tenantId, Guid userId, string code, string redirectUri, CancellationToken cancellationToken = default);

    /// <summary>Incremental pull of new/changed messages since the mailbox's stored sync state; matches threads to clients/matters via IEmailMatchingService.</summary>
    Task PullChangesAsync(Guid tenantId, Guid mailboxId, CancellationToken cancellationToken = default);

    /// <summary>Sends via the provider's own API (so the message lands in the user's real Sent folder) and returns the provider message id.</summary>
    Task<string> SendAsync(Guid tenantId, Guid mailboxId, EmailSendRequest request, CancellationToken cancellationToken = default);

    Task DisconnectAsync(Guid tenantId, Guid mailboxId, CancellationToken cancellationToken = default);
}

public sealed record EmailSendRequest(IReadOnlyList<string> ToAddresses, string Subject, string BodyHtml, IReadOnlyList<EmailAttachmentInput>? Attachments, string? InReplyToMessageIdHdr);

public sealed record EmailAttachmentInput(Guid DocumentId, string Filename, string Mime, byte[] Content);
