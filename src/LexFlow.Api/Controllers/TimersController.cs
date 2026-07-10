using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.TimeTracking;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.TimeTracking;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 9 timers — PRD §17. AC-T1: server-anchored, single source of truth.</summary>
[ApiController]
[Route("api/v1/timers")]
public sealed class TimersController(IMediator mediator) : ControllerBase
{
    [HttpPost("start")]
    [RequirePermission("time_entries.create.own")]
    public async Task<IActionResult> Start([FromBody] StartTimerRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<RunningTimerDto>.Of(await mediator.Send(new StartTimerCommand(request.MatterId, request.ActivityCodeId, request.ContextRef), cancellationToken)));

    [HttpPost("pause")]
    [RequirePermission("time_entries.create.own")]
    public async Task<IActionResult> Pause(CancellationToken cancellationToken)
        => Ok(ApiResponse<RunningTimerDto>.Of(await mediator.Send(new PauseTimerCommand(), cancellationToken)));

    [HttpPost("resume")]
    [RequirePermission("time_entries.create.own")]
    public async Task<IActionResult> Resume(CancellationToken cancellationToken)
        => Ok(ApiResponse<RunningTimerDto>.Of(await mediator.Send(new ResumeTimerCommand(), cancellationToken)));

    [HttpPost("stop")]
    [RequirePermission("time_entries.create.own")]
    public async Task<IActionResult> Stop([FromBody] StopTimerRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<TimeEntryDto>.Of(await mediator.Send(new StopTimerCommand(request.Billable, request.Narrative, request.InternalNote, request.ActivityCodeId), cancellationToken)));

    [HttpGet("current")]
    [RequirePermission("time_entries.read.own")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
        => Ok(ApiResponse<RunningTimerDto?>.Of(await mediator.Send(new GetCurrentTimerQuery(), cancellationToken)));
}

public sealed record StartTimerRequest(Guid? MatterId, Guid? ActivityCodeId, string? ContextRef);

public sealed record StopTimerRequest(bool Billable, string? Narrative, string? InternalNote, Guid? ActivityCodeId);
