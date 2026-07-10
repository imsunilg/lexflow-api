using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Kb;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Kb;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 12: tags, collections, bookmarks — PRD §17.</summary>
[ApiController]
[Route("api/v1/kb")]
public sealed class KbTaxonomyController(IMediator mediator) : ControllerBase
{
    [HttpGet("tags")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> GetTags(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<KbTagDto>>.Of(await mediator.Send(new GetKbTagsQuery(), cancellationToken)));

    [HttpPost("tags/attach")]
    [RequirePermission("kb.contribute.all")]
    public async Task<IActionResult> AttachTag([FromBody] KbTagRefRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new AttachKbTagCommand(request.KbRefKind, request.KbRefId, request.TagName), cancellationToken);
        return NoContent();
    }

    [HttpPost("tags/detach")]
    [RequirePermission("kb.contribute.all")]
    public async Task<IActionResult> DetachTag([FromBody] KbTagRefRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new DetachKbTagCommand(request.KbRefKind, request.KbRefId, request.TagName), cancellationToken);
        return NoContent();
    }

    [HttpPost("collections")]
    [RequirePermission("kb.contribute.all")]
    public async Task<IActionResult> CreateCollection([FromBody] CreateKbCollectionRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbCollectionDto>.Of(await mediator.Send(new CreateKbCollectionCommand(request.Name, request.Description), cancellationToken)));

    [HttpGet("collections")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> GetCollections(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<KbCollectionDto>>.Of(await mediator.Send(new GetKbCollectionsQuery(), cancellationToken)));

    [HttpPost("collections/{id:guid}/items")]
    [RequirePermission("kb.contribute.all")]
    public async Task<IActionResult> AddToCollection(Guid id, [FromBody] KbRefRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new AddToKbCollectionCommand(id, request.KbRefKind, request.KbRefId), cancellationToken);
        return NoContent();
    }

    [HttpGet("collections/{id:guid}/items")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> GetCollectionItems(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<KbCollectionItemDto>>.Of(await mediator.Send(new GetKbCollectionItemsQuery(id), cancellationToken)));

    [HttpPost("bookmarks")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> AddBookmark([FromBody] KbRefRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<KbBookmarkDto>.Of(await mediator.Send(new AddKbBookmarkCommand(request.KbRefKind, request.KbRefId), cancellationToken)));

    [HttpDelete("bookmarks")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> RemoveBookmark([FromQuery] string kbRefKind, [FromQuery] Guid kbRefId, CancellationToken cancellationToken)
    {
        await mediator.Send(new RemoveKbBookmarkCommand(kbRefKind, kbRefId), cancellationToken);
        return NoContent();
    }

    [HttpGet("bookmarks")]
    [RequirePermission("kb.read.all")]
    public async Task<IActionResult> GetBookmarks(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<KbBookmarkDto>>.Of(await mediator.Send(new GetKbBookmarksQuery(), cancellationToken)));
}

public sealed record KbRefRequest(string KbRefKind, Guid KbRefId);

public sealed record KbTagRefRequest(string KbRefKind, Guid KbRefId, string TagName);

public sealed record CreateKbCollectionRequest(string Name, string? Description);
