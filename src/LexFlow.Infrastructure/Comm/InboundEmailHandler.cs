using System.Text.Json;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Comm;

/// <summary>
/// The BCC-dropbox/provider-sync inbound handler, split out of EmailService so the per-provider
/// IEmailSync implementations (which call this from PullChangesAsync) and WebhookRouter can
/// depend on just this slice — see IInboundEmailHandler's doc comment for why depending on the
/// full IEmailService from here would be a circular DI graph. This class deliberately has no
/// dependency on IEmailSync/IEnumerable&lt;IEmailSync&gt;.
/// </summary>
public sealed class InboundEmailHandler(LexFlowDbContext db) : IInboundEmailHandler
{
    /// <inheritdoc cref="IInboundEmailHandler.HandleInboundAsync"/>
    public async Task<EmailThreadDto> HandleInboundAsync(Guid tenantId, InboundEmailInput input, CancellationToken cancellationToken = default)
    {
        var existing = await db.EmailMessages.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.MessageIdHdr == input.MessageIdHdr, cancellationToken);
        if (existing?.ThreadId is not null)
        {
            // Duplicate delivery of an already-filed message (e.g. re-poll before sync
            // state advances) — idempotent no-op, matches the loop-guard edge case.
            var alreadyFiledThread = await db.EmailThreads.SingleAsync(t => t.Id == existing.ThreadId, cancellationToken);
            return EmailDtoMapper.ToDto(alreadyFiledThread);
        }

        EmailThread? thread = null;
        if (input.InReplyTo is not null)
        {
            var parent = await db.EmailMessages.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.MessageIdHdr == input.InReplyTo, cancellationToken);
            if (parent?.ThreadId is not null)
            {
                thread = await db.EmailThreads.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == parent.ThreadId, cancellationToken);
            }
        }

        Guid? matterId = null;
        Guid? clientId = null;

        if (thread is not null)
        {
            matterId = thread.MatterId;
            clientId = thread.ClientId;
        }
        else
        {
            (matterId, clientId) = await MatchSenderAsync(tenantId, input.FromAddr, cancellationToken);
            thread = new EmailThread(tenantId, input.Subject, matterId, clientId, mailboxId: null);
            await db.EmailThreads.AddAsync(thread, cancellationToken);
        }

        thread.TouchLastMessageAt(input.SentAt);

        var message = new EmailMessage(tenantId, thread.Id, input.MessageIdHdr, input.InReplyTo, "Inbound", input.FromAddr, JsonSerializer.Serialize(input.ToAddrs), input.Subject, SanitizeHtml(input.BodyHtml), input.Attachments is { Count: > 0 }, input.SentAt, matterId, clientId);
        await db.EmailMessages.AddAsync(message, cancellationToken);

        if (input.Attachments is { Count: > 0 })
        {
            foreach (var attachment in input.Attachments)
            {
                await db.EmailAttachments.AddAsync(new EmailAttachment(tenantId, message.Id, message.SentAt, attachment.DocumentId, attachment.Filename, attachment.Content.LongLength, attachment.Mime), cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return EmailDtoMapper.ToDto(thread);
    }

    /// <summary>AC-CM3: exactly one client match (by crm.clients.email or any crm.client_contacts.email) with exactly one Open matter auto-files; anything else (0 or 2+ candidates) is left unmatched for Triage.</summary>
    private async Task<(Guid? MatterId, Guid? ClientId)> MatchSenderAsync(Guid tenantId, string fromAddr, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fromAddr))
        {
            return (null, null);
        }

        var directMatches = await db.Clients.Where(c => c.TenantId == tenantId && c.Email == fromAddr).Select(c => c.Id).ToListAsync(cancellationToken);
        var contactMatches = await db.ClientContacts.Where(c => c.TenantId == tenantId && c.Email == fromAddr).Select(c => c.ClientId).ToListAsync(cancellationToken);
        var candidateClientIds = directMatches.Concat(contactMatches).Distinct().ToList();

        if (candidateClientIds.Count != 1)
        {
            return (null, null);
        }

        var clientId = candidateClientIds[0];
        var openMatters = await db.Matters.Where(m => m.TenantId == tenantId && m.ClientId == clientId && m.Status == "Open").Select(m => m.Id).ToListAsync(cancellationToken);

        return openMatters.Count == 1 ? (openMatters[0], clientId) : ((Guid?)null, clientId);
    }

    /// <summary>Allow-list sanitizer per §27 (p, br, b, i, u, ul, ol, li, a[href https], h1-h4, blockquote, table basics) — a minimal, dependency-free implementation: strips every tag not on the allow-list, keeping their text content.</summary>
    private static string SanitizeHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return html;
        }

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "p", "br", "b", "i", "u", "ul", "ol", "li", "a", "h1", "h2", "h3", "h4", "blockquote", "table", "tr", "td", "th" };
        return System.Text.RegularExpressions.Regex.Replace(html, "<(/?)(\\w+)([^>]*)>", match =>
        {
            var tag = match.Groups[2].Value;
            if (!allowed.Contains(tag))
            {
                return string.Empty;
            }

            if (string.Equals(tag, "a", StringComparison.OrdinalIgnoreCase) && match.Groups[1].Value == string.Empty)
            {
                var hrefMatch = System.Text.RegularExpressions.Regex.Match(match.Groups[3].Value, "href\\s*=\\s*\"(https://[^\"]*)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                return hrefMatch.Success ? $"<a href=\"{hrefMatch.Groups[1].Value}\">" : "<a>";
            }

            return $"<{match.Groups[1].Value}{tag}>";
        });
    }
}
