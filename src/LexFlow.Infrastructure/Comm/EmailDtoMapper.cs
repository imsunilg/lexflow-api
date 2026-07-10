using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using System.Text.Json;

namespace LexFlow.Infrastructure.Comm;

/// <summary>Shared entity-to-DTO mapping used by both EmailService and InboundEmailHandler (split apart to avoid a circular DI dependency — see IInboundEmailHandler's doc comment).</summary>
internal static class EmailDtoMapper
{
    public static EmailThreadDto ToDto(EmailThread t) => new(t.Id, t.Subject, t.MatterId, t.ClientId, t.MailboxId, t.LastMessageAt);

    public static EmailMessageDto ToDto(EmailMessage m) => new(m.Id, m.ThreadId, m.MessageIdHdr, m.Direction, m.FromAddr, JsonSerializer.Deserialize<List<string>>(m.ToAddrsJson) ?? [], m.Subject, m.BodyHtmlSanitized, m.HasAttachments, m.SentAt);
}
