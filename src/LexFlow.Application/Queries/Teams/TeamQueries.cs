using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Teams;

public sealed record GetTeamsQuery : IRequest<IReadOnlyList<TeamDto>>;

public sealed class GetTeamsQueryHandler(ITeamService teamService, ICurrentUserService currentUser) : IRequestHandler<GetTeamsQuery, IReadOnlyList<TeamDto>>
{
    public Task<IReadOnlyList<TeamDto>> Handle(GetTeamsQuery request, CancellationToken cancellationToken)
        => teamService.GetAllAsync(currentUser.TenantId!.Value, cancellationToken);
}

public sealed record GetTeamQuery(Guid TeamId) : IRequest<TeamDto>;

public sealed class GetTeamQueryHandler(ITeamService teamService, ICurrentUserService currentUser) : IRequestHandler<GetTeamQuery, TeamDto>
{
    public async Task<TeamDto> Handle(GetTeamQuery request, CancellationToken cancellationToken)
        => await teamService.GetByIdAsync(currentUser.TenantId!.Value, request.TeamId, cancellationToken)
           ?? throw new NotFoundException("Team", request.TeamId);
}
