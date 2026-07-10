using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Kb;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Kb;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 12: Acts/Sections — PRD §17. AC-KB1 (direct section jump) / AC-KB3 (as-on-date historical rendering).</summary>
[ApiController]
[Route("api/v1/kb")]
public sealed class KbActsController(IMediator mediator) : ControllerBase
{
    [HttpPost("acts")]
    [RequirePermission("kb.contribute.all")]
    public async Task<IActionResult> CreateAct([FromBody] CreateKbActRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbActDto>.Of(await mediator.Send(new CreateKbActCommand(request.Name, request.ShortCode, request.Jurisdiction, request.Year), cancellationToken)));

    [HttpPut("acts/{id:guid}")]
    [RequirePermission("kb.contribute.all")]
    public async Task<IActionResult> UpdateAct(Guid id, [FromBody] CreateKbActRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbActDto>.Of(await mediator.Send(new UpdateKbActCommand(id, request.Name, request.ShortCode, request.Jurisdiction, request.Year), cancellationToken)));

    [HttpGet("acts")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> GetActs(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<KbActDto>>.Of(await mediator.Send(new GetKbActsQuery(), cancellationToken)));

    [HttpGet("acts/{id:guid}")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> GetAct(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbActDto?>.Of(await mediator.Send(new GetKbActQuery(id), cancellationToken)));

    [HttpGet("acts/{id:guid}/sections")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> GetSections(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<KbActSectionDto>>.Of(await mediator.Send(new GetKbActSectionsQuery(id), cancellationToken)));

    [HttpPost("acts/{id:guid}/sections")]
    [RequirePermission("kb.contribute.all")]
    public async Task<IActionResult> CreateSection(Guid id, [FromBody] CreateKbActSectionRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbActSectionDto>.Of(await mediator.Send(new CreateKbActSectionCommand(id, request.ParentId, request.Number, request.Title, request.Body, request.EffectiveFrom), cancellationToken)));

    [HttpGet("sections/{id:guid}")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> GetSection(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbActSectionDto?>.Of(await mediator.Send(new GetKbActSectionQuery(id), cancellationToken)));

    /// <summary>AC-KB1: "IPC 420" -&gt; direct section jump.</summary>
    [HttpGet("sections/lookup")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> LookupSection([FromQuery] string act, [FromQuery] string number, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbActSectionDto?>.Of(await mediator.Send(new LookupKbActSectionQuery(act, number), cancellationToken)));

    /// <summary>AC-KB3: as-on-date view of an amended section.</summary>
    [HttpGet("acts/{actId:guid}/sections/{number}/as-of")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> GetSectionAsOf(Guid actId, string number, [FromQuery] DateOnly asOf, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbActSectionDto?>.Of(await mediator.Send(new GetKbActSectionAsOfQuery(actId, number, asOf), cancellationToken)));

    [HttpGet("acts/{actId:guid}/sections/{number}/history")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> GetSectionHistory(Guid actId, string number, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<KbActSectionDto>>.Of(await mediator.Send(new GetKbActSectionHistoryQuery(actId, number), cancellationToken)));

    [HttpPost("sections/{id:guid}/amend")]
    [RequirePermission("kb.contribute.all")]
    public async Task<IActionResult> AmendSection(Guid id, [FromBody] AmendKbActSectionRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbActSectionDto>.Of(await mediator.Send(new AmendKbActSectionCommand(id, request.NewTitle, request.NewBody, request.AmendedOn), cancellationToken)));
}

public sealed record CreateKbActRequest(string Name, string? ShortCode, string? Jurisdiction, int? Year);

public sealed record CreateKbActSectionRequest(Guid? ParentId, string Number, string? Title, string? Body, DateOnly? EffectiveFrom);

public sealed record AmendKbActSectionRequest(string? NewTitle, string? NewBody, DateOnly AmendedOn);
