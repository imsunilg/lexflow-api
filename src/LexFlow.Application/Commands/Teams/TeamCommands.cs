using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Teams;

/// <summary>CRUD /api/v1/teams (PRD §17).</summary>
public sealed record CreateTeamCommand(string Name, Guid? LeadUserId, IReadOnlyList<Guid> MemberUserIds) : IRequest<TeamDto>;

public sealed class CreateTeamCommandHandler(ITeamService teamService, ICurrentUserService currentUser) : IRequestHandler<CreateTeamCommand, TeamDto>
{
    public Task<TeamDto> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
        => teamService.CreateAsync(currentUser.TenantId!.Value, request.Name, request.LeadUserId, request.MemberUserIds, cancellationToken);
}

public sealed record UpdateTeamCommand(Guid TeamId, string Name, Guid? LeadUserId, IReadOnlyList<Guid> MemberUserIds) : IRequest<TeamDto>;

public sealed class UpdateTeamCommandHandler(ITeamService teamService, ICurrentUserService currentUser) : IRequestHandler<UpdateTeamCommand, TeamDto>
{
    public Task<TeamDto> Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
        => teamService.UpdateAsync(currentUser.TenantId!.Value, request.TeamId, request.Name, request.LeadUserId, request.MemberUserIds, cancellationToken);
}

public sealed record DeleteTeamCommand(Guid TeamId) : IRequest;

public sealed class DeleteTeamCommandHandler(ITeamService teamService, ICurrentUserService currentUser) : IRequestHandler<DeleteTeamCommand>
{
    public async Task Handle(DeleteTeamCommand request, CancellationToken cancellationToken)
        => await teamService.DeleteAsync(currentUser.TenantId!.Value, request.TeamId, cancellationToken);
}
