using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Dunning;

public sealed record GetDunningSchedulesQuery : IRequest<IReadOnlyList<DunningScheduleDto>>;

public sealed class GetDunningSchedulesQueryHandler(IDunningService service, ICurrentUserService currentUser) : IRequestHandler<GetDunningSchedulesQuery, IReadOnlyList<DunningScheduleDto>>
{
    public Task<IReadOnlyList<DunningScheduleDto>> Handle(GetDunningSchedulesQuery request, CancellationToken cancellationToken)
        => service.GetSchedulesAsync(currentUser.TenantId!.Value, cancellationToken);
}
