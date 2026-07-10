using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Comm;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Comm;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 11 calls — PRD §17.</summary>
[ApiController]
[Route("api/v1/comm/calls")]
public sealed class CommCallsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission("comm.calls.manage")]
    public async Task<IActionResult> Log([FromBody] LogCallRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<CallLogDto>.Of(await mediator.Send(new LogCallCommand(request.ClientId, request.MatterId, request.UserId, request.Direction, request.DurationSec, request.Summary, request.CreateFollowUpTask, request.FollowUpTaskTitle), cancellationToken)));

    [HttpPost("click-to-call")]
    [RequirePermission("comm.calls.manage")]
    public async Task<IActionResult> ClickToCall([FromBody] ClickToCallRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<CallLogDto>.Of(await mediator.Send(new ClickToCallCommand(request.ClientId, request.MatterId, request.ToNumber, request.ConsentGiven), cancellationToken)));

    [HttpGet("clients/{clientId:guid}")]
    [RequirePermission("comm.calls.read")]
    public async Task<IActionResult> GetForClient(Guid clientId, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<CallLogDto>>.Of(await mediator.Send(new GetCallsForClientQuery(clientId), cancellationToken)));
}

public sealed record LogCallRequest(Guid? ClientId, Guid? MatterId, Guid? UserId, string Direction, int DurationSec, string? Summary, bool CreateFollowUpTask, string? FollowUpTaskTitle);

public sealed record ClickToCallRequest(Guid? ClientId, Guid? MatterId, string ToNumber, bool ConsentGiven);
