using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Ai;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Ai;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 16: AI Gateway — the 12 AI features, transcription pipeline, feedback, and quota status. Every action requires ai.use.own at minimum; feature-specific access (a document/matter the caller can't read) is enforced inside IAiAssistantService via IAiRetrievalGuard, not here.</summary>
[ApiController]
[Route("api/v1/ai")]
public sealed class AiController(IMediator mediator) : ControllerBase
{
    [HttpPost("chat")]
    [RequirePermission("ai.use.own")]
    public async Task<IActionResult> Chat([FromBody] AiChatRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<AiChatResponse>.Of(await mediator.Send(new AiChatCommand(request), cancellationToken)));

    [HttpPost("summarize/document/{id:guid}")]
    [RequirePermission("ai.use.own")]
    public async Task<IActionResult> SummarizeDocument(Guid id, [FromQuery] string? lengthPreset, CancellationToken cancellationToken)
        => Ok(ApiResponse<AiDocumentSummaryResponse>.Of(await mediator.Send(new AiSummarizeDocumentCommand(id, lengthPreset), cancellationToken)));

    [HttpPost("summarize/matter/{id:guid}")]
    [RequirePermission("ai.use.own")]
    public async Task<IActionResult> SummarizeMatter(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<AiMatterSummaryResponse>.Of(await mediator.Send(new AiSummarizeMatterCommand(id), cancellationToken)));

    [HttpPost("contracts/{docId:guid}/review")]
    [RequirePermission("ai.use.own")]
    public async Task<IActionResult> ReviewContract(Guid docId, CancellationToken cancellationToken)
        => Ok(ApiResponse<AiContractReviewResponse>.Of(await mediator.Send(new AiReviewContractCommand(docId), cancellationToken)));

    [HttpPost("draft")]
    [RequirePermission("ai.use.own")]
    public async Task<IActionResult> Draft([FromBody] AiDraftRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<AiDraftResponse>.Of(await mediator.Send(new AiDraftCommand(request), cancellationToken)));

    [HttpPost("research")]
    [RequirePermission("ai.use.own")]
    public async Task<IActionResult> Research([FromBody] AiResearchRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<AiResearchResponse>.Of(await mediator.Send(new AiResearchCommand(request.Question, request.WebGroundedMode), cancellationToken)));

    [HttpGet("matters/{id:guid}/similar")]
    [RequirePermission("ai.use.own")]
    public async Task<IActionResult> GetSimilarMatters(Guid id, [FromQuery] int topN, CancellationToken cancellationToken)
        => Ok(ApiResponse<AiSimilarMattersResponse>.Of(await mediator.Send(new AiSimilarMattersCommand(id, topN <= 0 ? 5 : topN), cancellationToken)));

    [HttpGet("cases/{id:guid}/hearing-prediction")]
    [RequirePermission("ai.use.own")]
    public async Task<IActionResult> PredictNextHearing(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<AiHearingPredictionResponse>.Of(await mediator.Send(new AiHearingPredictionCommand(id), cancellationToken)));

    [HttpGet("matters/{id:guid}/risk")]
    [RequirePermission("ai.use.own")]
    public async Task<IActionResult> GetRiskAnalysis(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<AiRiskAnalysisResponse>.Of(await mediator.Send(new AiRiskAnalysisCommand(id), cancellationToken)));

    [HttpPost("email")]
    [RequirePermission("ai.use.own")]
    public async Task<IActionResult> GenerateEmail([FromBody] AiEmailGenerateRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<AiEmailGenerateResponse>.Of(await mediator.Send(new AiGenerateEmailCommand(request), cancellationToken)));

    [HttpPost("transcriptions/{id:guid}/summarize")]
    [RequirePermission("ai.use.own")]
    public async Task<IActionResult> SummarizeMeeting(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<AiMeetingSummaryResponse>.Of(await mediator.Send(new AiSummarizeMeetingCommand(id), cancellationToken)));

    [HttpPost("transcribe")]
    [RequirePermission("ai.use.own")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> Transcribe([FromForm] Guid? matterId, [FromForm] string? language, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        var command = new AiRequestTranscriptionCommand(matterId, stream.ToArray(), file.FileName, file.ContentType, language);
        return Ok(ApiResponse<AiTranscriptionDto>.Of(await mediator.Send(command, cancellationToken)));
    }

    [HttpGet("transcriptions/{id:guid}")]
    [RequirePermission("ai.use.own")]
    public async Task<IActionResult> GetTranscription(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAiTranscriptionQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(ApiResponse<AiTranscriptionDto>.Of(result));
    }

    [HttpPost("interactions/{id:guid}/feedback")]
    [RequirePermission("ai.use.own")]
    public async Task<IActionResult> RecordFeedback(Guid id, [FromBody] AiFeedbackRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new AiRecordFeedbackCommand(id, request.Rating, request.Reason), cancellationToken);
        return NoContent();
    }

    [HttpGet("quota")]
    [RequirePermission("ai.use.own")]
    public async Task<IActionResult> GetQuota(CancellationToken cancellationToken)
        => Ok(ApiResponse<AiQuotaStatus>.Of(await mediator.Send(new GetAiQuotaStatusQuery(), cancellationToken)));
}

public sealed record AiResearchRequest(string Question, bool WebGroundedMode);

public sealed record AiFeedbackRequest(int Rating, string? Reason);
