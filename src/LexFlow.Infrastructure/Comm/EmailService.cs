using System.Text.Json;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Comm;

/// <summary>
/// Module 11 email — thread/message persistence, compose+send orchestration (delegates
/// the actual provider call to IEmailSync), and the BCC-dropbox inbound handler.
/// AC-CM3: sender-email match against crm.clients/crm.client_contacts; if exactly one
/// client and exactly one open matter for that client match, the thread files itself
/// directly — otherwise (no match, or the shared-inbox edge case of two clients using
/// the same address) it's created with no matter/client link, which *is* the Triage
/// queue (GetTriageQueueAsync just lists threads in that state).
/// </summary>
public sealed class EmailService(LexFlowDbContext db, IEnumerable<IEmailSync> syncProviders, IInboundEmailHandler inboundEmailHandler) : IEmailService
{
    public async Task<EmailThreadDto> SendAsync(Guid tenantId, Guid? actorId, Guid mailboxId, EmailSendRequest request, Guid? matterId, Guid? clientId, CancellationToken cancellationToken = default)
    {
        var mailbox = await db.Mailboxes.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken)
            ?? throw new NotFoundException(nameof(Mailbox), mailboxId);

        var syncProvider = syncProviders.SingleOrDefault(p => string.Equals(p.Provider, mailbox.Provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("EmailSyncProvider", mailbox.Provider);

        var providerMessageId = await syncProvider.SendAsync(tenantId, mailboxId, request, cancellationToken);
        var messageIdHdr = $"<{providerMessageId}@{mailbox.Provider}.lexflow>";

        EmailThread? thread = null;
        if (request.InReplyToMessageIdHdr is not null)
        {
            var parent = await db.EmailMessages.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.MessageIdHdr == request.InReplyToMessageIdHdr, cancellationToken);
            if (parent?.ThreadId is not null)
            {
                thread = await db.EmailThreads.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == parent.ThreadId, cancellationToken);
            }
        }

        if (thread is null)
        {
            thread = new EmailThread(tenantId, request.Subject, matterId, clientId, mailboxId);
            await db.EmailThreads.AddAsync(thread, cancellationToken);
        }

        if (matterId.HasValue)
        {
            thread.LinkToMatter(matterId.Value, clientId);
        }

        var sentAt = DateTimeOffset.UtcNow;
        thread.TouchLastMessageAt(sentAt);

        var message = new EmailMessage(tenantId, thread.Id, messageIdHdr, request.InReplyToMessageIdHdr, "Outbound", mailbox.EmailAddress, JsonSerializer.Serialize(request.ToAddresses), request.Subject, request.BodyHtml, request.Attachments is { Count: > 0 }, sentAt, matterId, clientId);
        await db.EmailMessages.AddAsync(message, cancellationToken);

        if (request.Attachments is { Count: > 0 })
        {
            foreach (var attachment in request.Attachments)
            {
                await db.EmailAttachments.AddAsync(new EmailAttachment(tenantId, message.Id, message.SentAt, attachment.DocumentId, attachment.Filename, attachment.Content.LongLength, attachment.Mime), cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(thread);
    }

    public async Task<IReadOnlyList<EmailThreadDto>> GetThreadsAsync(Guid tenantId, Guid? matterId, Guid? clientId, CancellationToken cancellationToken = default)
    {
        var query = db.EmailThreads.Where(t => t.TenantId == tenantId).AsQueryable();
        if (matterId.HasValue)
        {
            query = query.Where(t => t.MatterId == matterId.Value);
        }

        if (clientId.HasValue)
        {
            query = query.Where(t => t.ClientId == clientId.Value);
        }

        var threads = await query.OrderByDescending(t => t.LastMessageAt).ToListAsync(cancellationToken);
        return threads.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<EmailThreadDto>> GetTriageQueueAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var threads = await db.EmailThreads
            .Where(t => t.TenantId == tenantId && t.MatterId == null && t.ClientId == null)
            .OrderByDescending(t => t.LastMessageAt)
            .ToListAsync(cancellationToken);
        return threads.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<EmailMessageDto>> GetMessagesAsync(Guid tenantId, Guid threadId, CancellationToken cancellationToken = default)
    {
        var messages = await db.EmailMessages.Where(m => m.TenantId == tenantId && m.ThreadId == threadId).OrderBy(m => m.SentAt).ToListAsync(cancellationToken);
        return messages.Select(ToDto).ToList();
    }

    public async Task<EmailThreadDto> LinkThreadToMatterAsync(Guid tenantId, Guid threadId, Guid matterId, Guid? clientId, CancellationToken cancellationToken = default)
    {
        var thread = await db.EmailThreads.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == threadId, cancellationToken)
            ?? throw new NotFoundException(nameof(EmailThread), threadId);

        thread.LinkToMatter(matterId, clientId);

        var threadMessages = await db.EmailMessages.Where(m => m.TenantId == tenantId && m.ThreadId == threadId).ToListAsync(cancellationToken);
        foreach (var message in threadMessages)
        {
            message.SetMatterLink(matterId, clientId);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(thread);
    }

    /// <summary>Delegates to InboundEmailHandler — see IInboundEmailHandler's doc comment for why this logic lives in a separate, non-circular dependency rather than inline here.</summary>
    public Task<EmailThreadDto> HandleInboundAsync(Guid tenantId, InboundEmailInput input, CancellationToken cancellationToken = default)
        => inboundEmailHandler.HandleInboundAsync(tenantId, input, cancellationToken);

    private static EmailThreadDto ToDto(EmailThread t) => EmailDtoMapper.ToDto(t);

    private static EmailMessageDto ToDto(EmailMessage m) => EmailDtoMapper.ToDto(m);
}
