using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Teams;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Teams;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>CRUD /api/v1/teams (PRD §17, Module 14).</summary>
[ApiController]
[Route("api/v1/teams")]
public sealed class TeamsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("users.read.all")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<TeamDto>>.Of(await mediator.Send(new GetTeamsQuery(), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission("users.read.all")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<TeamDto>.Of(await mediator.Send(new GetTeamQuery(id), cancellationToken)));

    [HttpPost]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Create([FromBody] CreateTeamCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<TeamDto>.Of(await mediator.Send(command, cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTeamRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<TeamDto>.Of(await mediator.Send(new UpdateTeamCommand(id, request.Name, request.LeadUserId, request.MemberUserIds), cancellationToken)));

    [HttpDelete("{id:guid}")]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteTeamCommand(id), cancellationToken);
        return NoContent();
    }
}

public sealed record UpdateTeamRequest(string Name, Guid? LeadUserId, IReadOnlyList<Guid> MemberUserIds);
