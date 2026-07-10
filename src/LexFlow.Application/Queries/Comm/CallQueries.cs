using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Comm;

public sealed record GetCallsForClientQuery(Guid ClientId) : IRequest<IReadOnlyList<CallLogDto>>;

public sealed class GetCallsForClientQueryHandler(ICallService callService, ICurrentUserService currentUser) : IRequestHandler<GetCallsForClientQuery, IReadOnlyList<CallLogDto>>
{
    public Task<IReadOnlyList<CallLogDto>> Handle(GetCallsForClientQuery request, CancellationToken cancellationToken)
        => callService.GetForClientAsync(currentUser.TenantId!.Value, request.ClientId, cancellationToken);
}
