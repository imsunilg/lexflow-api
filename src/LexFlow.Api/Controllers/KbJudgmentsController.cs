using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Kb;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Kb;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 12: Judgments/Case Laws — PRD §17. Validation Rules: judgment PDF ≤ 50 MB.</summary>
[ApiController]
[Route("api/v1/kb/judgments")]
public sealed class KbJudgmentsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission("kb.contribute.all")]
    [RequestSizeLimit(55_000_000)]
    public async Task<IActionResult> Upload([FromForm] UploadKbJudgmentForm form, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        var command = new UploadKbJudgmentCommand(form.Citation, form.NeutralCitation, form.CourtId, form.DecisionDate, form.Parties, form.Headnote, stream.ToArray(), file.FileName, file.ContentType);
        return Ok(ApiResponse<KbJudgmentDto>.Of(await mediator.Send(command, cancellationToken)));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbJudgmentDto?>.Of(await mediator.Send(new GetKbJudgmentQuery(id), cancellationToken)));

    [HttpGet]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<KbJudgmentDto>>.Of(await mediator.Send(new GetKbJudgmentsQuery(), cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("kb.contribute.all")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateKbJudgmentRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbJudgmentDto>.Of(await mediator.Send(new UpdateKbJudgmentCommand(id, request.NeutralCitation, request.CourtId, request.DecisionDate, request.Parties), cancellationToken)));

    [HttpPut("{id:guid}/headnote")]
    [RequirePermission("kb.contribute.all")]
    public async Task<IActionResult> UpdateHeadnote(Guid id, [FromBody] UpdateKbJudgmentHeadnoteRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbJudgmentDto>.Of(await mediator.Send(new UpdateKbJudgmentHeadnoteCommand(id, request.Headnote), cancellationToken)));

    /// <summary>Error Handling: "OCR fail on judgment -&gt; metadata-only searchable + retry."</summary>
    [HttpPost("{id:guid}/retry-extraction")]
    [RequirePermission("kb.contribute.all")]
    public async Task<IActionResult> RetryExtraction(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RetryKbJudgmentExtractionCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>AC-KB4: "back-link 'pinned in 3 matters'".</summary>
    [HttpGet("{id:guid}/pin-count")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> GetPinCount(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<int>.Of(await mediator.Send(new GetKbJudgmentPinCountQuery(id), cancellationToken)));
}

public sealed record UploadKbJudgmentForm(string Citation, string? NeutralCitation, Guid? CourtId, DateOnly? DecisionDate, string? Parties, string? Headnote);

public sealed record UpdateKbJudgmentRequest(string? NeutralCitation, Guid? CourtId, DateOnly? DecisionDate, string? Parties);

public sealed record UpdateKbJudgmentHeadnoteRequest(string? Headnote);
