using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Kb;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Kb;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 12: internal articles, draft -&gt; peer review -&gt; publish. AC-KB5: unreviewed article never visible to non-authors (enforced in the Application layer, not here).</summary>
[ApiController]
[Route("api/v1/kb/articles")]
public sealed class KbArticlesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission("kb.contribute.all")]
    public async Task<IActionResult> Create([FromBody] CreateKbArticleRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbArticleDto>.Of(await mediator.Send(new CreateKbArticleDraftCommand(request.Title, request.Body), cancellationToken)));

    [HttpGet]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<KbArticleDto>>.Of(await mediator.Send(new GetKbArticlesQuery(), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbArticleDto?>.Of(await mediator.Send(new GetKbArticleQuery(id), cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("kb.contribute.all")]
    public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] CreateKbArticleRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbArticleDto>.Of(await mediator.Send(new UpdateKbArticleDraftCommand(id, request.Title, request.Body), cancellationToken)));

    [HttpPost("{id:guid}/submit")]
    [RequirePermission("kb.contribute.all")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbArticleDto>.Of(await mediator.Send(new SubmitKbArticleForReviewCommand(id), cancellationToken)));

    [HttpPost("{id:guid}/assign-reviewer")]
    [RequirePermission("kb.review.all")]
    public async Task<IActionResult> AssignReviewer(Guid id, [FromBody] AssignKbArticleReviewerRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbArticleDto>.Of(await mediator.Send(new AssignKbArticleReviewerCommand(id, request.ReviewerId), cancellationToken)));

    /// <summary>One-shot reviewer action: assigns the caller as reviewer and publishes in the same call (see ApproveKbArticleCommand's own doc comment for why "approve" and "publish" collapse into one transition in this schema).</summary>
    [HttpPost("{id:guid}/approve")]
    [RequirePermission("kb.review.all")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveKbArticleRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbArticleDto>.Of(await mediator.Send(new ApproveKbArticleCommand(id, request.ReviewerId), cancellationToken)));

    [HttpPost("{id:guid}/publish")]
    [RequirePermission("kb.review.all")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbArticleDto>.Of(await mediator.Send(new PublishKbArticleCommand(id), cancellationToken)));

    [HttpPost("{id:guid}/send-back")]
    [RequirePermission("kb.review.all")]
    public async Task<IActionResult> SendBackToDraft(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbArticleDto>.Of(await mediator.Send(new SendKbArticleBackToDraftCommand(id), cancellationToken)));

    [HttpGet("{id:guid}/versions")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> GetVersions(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<KbArticleVersionDto>>.Of(await mediator.Send(new GetKbArticleVersionsQuery(id), cancellationToken)));
}

public sealed record CreateKbArticleRequest(string Title, string? Body);

public sealed record AssignKbArticleReviewerRequest(Guid ReviewerId);

public sealed record ApproveKbArticleRequest(Guid ReviewerId);
