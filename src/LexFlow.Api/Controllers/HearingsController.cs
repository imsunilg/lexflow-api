using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Cases;
using LexFlow.Application.Commands.Hearings;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Cases;
using LexFlow.Application.Queries.Hearings;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 5 hearings — PRD §17. AC-CC1/G-AC2 lives in <see cref="RecordOutcome"/>.</summary>
[ApiController]
[Route("api/v1/hearings")]
public sealed class HearingsController(IMediator mediator) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<HearingDto>.Of(await mediator.Send(new GetHearingQuery(id), cancellationToken)));

    /// <summary>AC-CC1: "recording an outcome with next date creates next hearing + reminders in one transaction."</summary>
    [HttpPost("{id:guid}/outcome")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> RecordOutcome(Guid id, [FromBody] RecordOutcomeRequest request, CancellationToken cancellationToken)
    {
        var command = new RecordHearingOutcomeCommand(
            id, request.Summary, request.AdjournReason,
            request.NextHearing?.Date, request.NextHearing?.Time, request.NextHearing?.Purpose,
            request.SineDie, request.Disposed, request.Orders, request.CreateComplianceTask);

        return Ok(ApiResponse<RecordOutcomeResult>.Of(await mediator.Send(command, cancellationToken)));
    }

    /// <summary>Cause list: GET /api/v1/hearings?date=&amp;courtId=&amp;lawyerId=. AC-CC2.</summary>
    [HttpGet]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetCauseList([FromQuery] DateOnly date, [FromQuery] Guid? courtId, [FromQuery] Guid? lawyerId, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<HearingDto>>.Of(await mediator.Send(new GetCauseListQuery(date, courtId, lawyerId), cancellationToken)));
}

/// <summary>Separate top-level resource per PRD §17: POST /api/v1/evidence/{id}/custody.</summary>
[ApiController]
[Route("api/v1/evidence")]
public sealed class EvidenceController(IMediator mediator) : ControllerBase
{
    [HttpPost("{id:guid}/custody")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> AddCustodyEvent(Guid id, [FromBody] AddCustodyEventRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<EvidenceCustodyLogDto>.Of(await mediator.Send(new AddEvidenceCustodyEventCommand(id, request.Action, request.Holder, request.Note), cancellationToken)));

    /// <summary>AC-CC5: complete custody chain.</summary>
    [HttpGet("{id:guid}/custody")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetCustodyChain(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<EvidenceCustodyLogDto>>.Of(await mediator.Send(new GetEvidenceCustodyChainQuery(id), cancellationToken)));
}

public sealed record RecordOutcomeRequest(string Summary, string? AdjournReason, NextHearingRequest? NextHearing, bool SineDie, bool Disposed, IReadOnlyList<CreateOrderInput>? Orders, bool CreateComplianceTask);

public sealed record NextHearingRequest(DateOnly Date, TimeOnly? Time, string? Purpose);

public sealed record AddCustodyEventRequest(string Action, string? Holder, string? Note);
