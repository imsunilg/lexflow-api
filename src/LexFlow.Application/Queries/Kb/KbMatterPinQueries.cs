using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Kb;

public sealed record GetKbMatterPinsQuery(Guid MatterId) : IRequest<IReadOnlyList<KbMatterPinDto>>;

public sealed class GetKbMatterPinsQueryHandler(IKbMatterPinService service, ICurrentUserService currentUser) : IRequestHandler<GetKbMatterPinsQuery, IReadOnlyList<KbMatterPinDto>>
{
    public Task<IReadOnlyList<KbMatterPinDto>> Handle(GetKbMatterPinsQuery request, CancellationToken cancellationToken)
        => service.GetForMatterAsync(currentUser.TenantId!.Value, request.MatterId, cancellationToken);
}
