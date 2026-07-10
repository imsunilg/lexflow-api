using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Comm;

/// <summary>GET /api/v1/comm/timeline?clientId=&amp;channels=.</summary>
public sealed record GetCommTimelineQuery(Guid? ClientId, IReadOnlyCollection<string>? Channels) : IRequest<IReadOnlyList<CommTimelineEntryDto>>;

public sealed class GetCommTimelineQueryHandler(ICommTimelineService timelineService, ICurrentUserService currentUser) : IRequestHandler<GetCommTimelineQuery, IReadOnlyList<CommTimelineEntryDto>>
{
    public Task<IReadOnlyList<CommTimelineEntryDto>> Handle(GetCommTimelineQuery request, CancellationToken cancellationToken)
        => timelineService.GetTimelineAsync(currentUser.TenantId!.Value, request.ClientId, request.Channels, cancellationToken);
}
