using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.TimeTracking;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.TimeTracking;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 9 timesheet + approval workflow — PRD §17. AC-T2/AC-T3.</summary>
[ApiController]
[Route("api/v1/time-entries")]
public sealed class TimeEntriesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission("time_entries.create.own")]
    public async Task<IActionResult> Create([FromBody] CreateTimeEntryRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<TimeEntryDto>.Of(await mediator.Send(new CreateTimeEntryCommand(request.MatterId, request.ActivityCodeId, request.EntryDate, request.StartedAt, request.DurationMin, request.Billable, request.Narrative, request.InternalNote), cancellationToken)));

    [HttpGet]
    [RequirePermission("time_entries.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId, [FromQuery] Guid? matterId, [FromQuery] string? status, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<TimeEntryDto>>.Of(await mediator.Send(new GetTimeEntriesQuery(userId, matterId, status, from, to), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission("time_entries.read.own")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<TimeEntryDto?>.Of(await mediator.Send(new GetTimeEntryQuery(id), cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("time_entries.update.own")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTimeEntryRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<TimeEntryDto>.Of(await mediator.Send(new UpdateTimeEntryCommand(id, request.MatterId, request.ActivityCodeId, request.EntryDate, request.DurationMin, request.Billable, request.Narrative, request.InternalNote), cancellationToken)));

    [HttpDelete("{id:guid}")]
    [RequirePermission("time_entries.delete.own")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteTimeEntryCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("submit")]
    [RequirePermission("time_entries.update.own")]
    public async Task<IActionResult> Submit([FromBody] TimeEntryIdsRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<TimeEntryDto>>.Of(await mediator.Send(new SubmitTimeEntriesCommand(request.Ids), cancellationToken)));

    [HttpPost("approve")]
    [RequirePermission("time_entries.approve.team")]
    public async Task<IActionResult> Approve([FromBody] ApproveTimeEntriesRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<TimeEntryDto>>.Of(await mediator.Send(new ApproveTimeEntriesCommand(request.Ids, request.ManualRateOverride), cancellationToken)));

    [HttpPost("reject")]
    [RequirePermission("time_entries.approve.team")]
    public async Task<IActionResult> Reject([FromBody] RejectTimeEntriesRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<TimeEntryDto>>.Of(await mediator.Send(new RejectTimeEntriesCommand(request.Ids, request.Comment), cancellationToken)));
}

public sealed record CreateTimeEntryRequest(Guid MatterId, Guid? ActivityCodeId, DateOnly EntryDate, DateTimeOffset? StartedAt, int DurationMin, bool Billable, string? Narrative, string? InternalNote);

public sealed record UpdateTimeEntryRequest(Guid MatterId, Guid? ActivityCodeId, DateOnly EntryDate, int DurationMin, bool Billable, string? Narrative, string? InternalNote);

public sealed record TimeEntryIdsRequest(IReadOnlyList<Guid> Ids);

public sealed record ApproveTimeEntriesRequest(IReadOnlyList<Guid> Ids, decimal? ManualRateOverride);

public sealed record RejectTimeEntriesRequest(IReadOnlyList<Guid> Ids, string? Comment);
