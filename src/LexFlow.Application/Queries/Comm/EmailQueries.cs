using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Comm;

/// <summary>GET /api/v1/comm/email/threads?matterId=&amp;clientId=.</summary>
public sealed record GetEmailThreadsQuery(Guid? MatterId, Guid? ClientId) : IRequest<IReadOnlyList<EmailThreadDto>>;

public sealed class GetEmailThreadsQueryHandler(IEmailService emailService, ICurrentUserService currentUser) : IRequestHandler<GetEmailThreadsQuery, IReadOnlyList<EmailThreadDto>>
{
    public Task<IReadOnlyList<EmailThreadDto>> Handle(GetEmailThreadsQuery request, CancellationToken cancellationToken)
        => emailService.GetThreadsAsync(currentUser.TenantId!.Value, request.MatterId, request.ClientId, cancellationToken);
}

/// <summary>AC-CM3: threads with no matter/client link yet.</summary>
public sealed record GetEmailTriageQueueQuery : IRequest<IReadOnlyList<EmailThreadDto>>;

public sealed class GetEmailTriageQueueQueryHandler(IEmailService emailService, ICurrentUserService currentUser) : IRequestHandler<GetEmailTriageQueueQuery, IReadOnlyList<EmailThreadDto>>
{
    public Task<IReadOnlyList<EmailThreadDto>> Handle(GetEmailTriageQueueQuery request, CancellationToken cancellationToken)
        => emailService.GetTriageQueueAsync(currentUser.TenantId!.Value, cancellationToken);
}

public sealed record GetEmailMessagesQuery(Guid ThreadId) : IRequest<IReadOnlyList<EmailMessageDto>>;

public sealed class GetEmailMessagesQueryHandler(IEmailService emailService, ICurrentUserService currentUser) : IRequestHandler<GetEmailMessagesQuery, IReadOnlyList<EmailMessageDto>>
{
    public Task<IReadOnlyList<EmailMessageDto>> Handle(GetEmailMessagesQuery request, CancellationToken cancellationToken)
        => emailService.GetMessagesAsync(currentUser.TenantId!.Value, request.ThreadId, cancellationToken);
}
