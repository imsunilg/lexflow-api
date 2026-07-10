using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Comm;

/// <summary>POST /api/v1/comm/email/accounts/connect — returns the provider's OAuth consent-screen URL.</summary>
public sealed record ConnectEmailAccountCommand(string Provider, string RedirectUri) : IRequest<string>;

public sealed class ConnectEmailAccountCommandHandler(IEnumerable<IEmailSync> syncProviders, ICurrentUserService currentUser) : IRequestHandler<ConnectEmailAccountCommand, string>
{
    public Task<string> Handle(ConnectEmailAccountCommand request, CancellationToken cancellationToken)
    {
        var provider = ResolveProvider(syncProviders, request.Provider);
        return Task.FromResult(provider.GetAuthorizationUrl(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.RedirectUri));
    }

    internal static IEmailSync ResolveProvider(IEnumerable<IEmailSync> providers, string name)
        => providers.SingleOrDefault(p => string.Equals(p.Provider, name, StringComparison.OrdinalIgnoreCase))
           ?? throw new Common.Exceptions.NotFoundException("EmailSyncProvider", name);
}

public sealed record HandleEmailOAuthCallbackCommand(string Provider, string Code, string RedirectUri) : IRequest<Guid>;

public sealed class HandleEmailOAuthCallbackCommandHandler(IEnumerable<IEmailSync> syncProviders, ICurrentUserService currentUser) : IRequestHandler<HandleEmailOAuthCallbackCommand, Guid>
{
    public Task<Guid> Handle(HandleEmailOAuthCallbackCommand request, CancellationToken cancellationToken)
    {
        var provider = ConnectEmailAccountCommandHandler.ResolveProvider(syncProviders, request.Provider);
        return provider.HandleOAuthCallbackAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Code, request.RedirectUri, cancellationToken);
    }
}

/// <summary>POST /api/v1/comm/email/send. AC-CM1.</summary>
public sealed record SendEmailCommand(Guid MailboxId, IReadOnlyList<string> ToAddresses, string Subject, string BodyHtml, IReadOnlyList<EmailAttachmentInput>? Attachments, string? InReplyToMessageIdHdr, Guid? MatterId, Guid? ClientId) : IRequest<EmailThreadDto>;

public sealed class SendEmailCommandHandler(IEmailService emailService, ICurrentUserService currentUser) : IRequestHandler<SendEmailCommand, EmailThreadDto>
{
    public Task<EmailThreadDto> Handle(SendEmailCommand request, CancellationToken cancellationToken)
        => emailService.SendAsync(currentUser.TenantId!.Value, currentUser.UserId, request.MailboxId, new EmailSendRequest(request.ToAddresses, request.Subject, request.BodyHtml, request.Attachments, request.InReplyToMessageIdHdr), request.MatterId, request.ClientId, cancellationToken);
}

/// <summary>POST /api/v1/comm/email/threads/{id}/link {matterId}.</summary>
public sealed record LinkEmailThreadCommand(Guid ThreadId, Guid MatterId, Guid? ClientId) : IRequest<EmailThreadDto>;

public sealed class LinkEmailThreadCommandHandler(IEmailService emailService, ICurrentUserService currentUser) : IRequestHandler<LinkEmailThreadCommand, EmailThreadDto>
{
    public Task<EmailThreadDto> Handle(LinkEmailThreadCommand request, CancellationToken cancellationToken)
        => emailService.LinkThreadToMatterAsync(currentUser.TenantId!.Value, request.ThreadId, request.MatterId, request.ClientId, cancellationToken);
}
