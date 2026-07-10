using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Documents;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Documents;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 7 Document Management (PRD §17). AC-DOC3: confidentiality gating is enforced inside the Application/Infrastructure layer, not here.</summary>
[ApiController]
[Route("api/v1/documents")]
public sealed class DocumentsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission("documents.manage.all")]
    [RequestSizeLimit(110_000_000)]
    public async Task<IActionResult> Create([FromForm] UploadDocumentForm form, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        var tagNames = form.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var command = new CreateDocumentCommand(form.FolderId, form.MatterId, form.ClientId, form.CaseId, form.Title, form.DocType, form.Confidentiality, tagNames, stream.ToArray(), file.FileName, file.ContentType);
        return Ok(ApiResponse<DocumentDto>.Of(await mediator.Send(command, cancellationToken)));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("documents.read.all")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<DocumentDto>.Of(await mediator.Send(new GetDocumentQuery(id), cancellationToken)));

    [HttpGet]
    [RequirePermission("documents.read.all")]
    public async Task<IActionResult> GetAll([FromQuery] Guid? matterId, [FromQuery] Guid? folderId, [FromQuery] string? docType, [FromQuery] string? q, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<DocumentDto>>.Of(await mediator.Send(new GetDocumentsQuery(matterId, folderId, docType, q), cancellationToken)));

    [HttpPatch("{id:guid}")]
    [RequirePermission("documents.manage.all")]
    public async Task<IActionResult> UpdateMetadata(Guid id, [FromBody] UpdateDocumentMetadataRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<DocumentDto>.Of(await mediator.Send(new UpdateDocumentMetadataCommand(id, request.Title, request.DocType, request.Confidentiality, request.FolderId), cancellationToken)));

    [HttpDelete("{id:guid}")]
    [RequirePermission("documents.manage.all")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteDocumentCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/versions")]
    [RequirePermission("documents.manage.all")]
    [RequestSizeLimit(110_000_000)]
    public async Task<IActionResult> AddVersion(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        return Ok(ApiResponse<DocumentVersionDto>.Of(await mediator.Send(new AddDocumentVersionCommand(id, stream.ToArray(), file.FileName, file.ContentType), cancellationToken)));
    }

    [HttpGet("{id:guid}/versions")]
    [RequirePermission("documents.read.all")]
    public async Task<IActionResult> GetVersions(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<DocumentVersionDto>>.Of(await mediator.Send(new GetDocumentVersionsQuery(id), cancellationToken)));

    /// <summary>AC-DOC2: "current pointer switchable with permission".</summary>
    [HttpPost("{id:guid}/versions/{v:int}/restore")]
    [RequirePermission("documents.manage.all")]
    public async Task<IActionResult> RestoreVersion(Guid id, int v, CancellationToken cancellationToken)
        => Ok(ApiResponse<DocumentVersionDto>.Of(await mediator.Send(new RestoreDocumentVersionCommand(id, v), cancellationToken)));

    [HttpGet("{id:guid}/download")]
    [RequirePermission("documents.read.all")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var url = await mediator.Send(new GetDocumentDownloadUrlQuery(id, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()), cancellationToken);
        return Redirect(url);
    }

    [HttpGet("{id:guid}/preview")]
    [RequirePermission("documents.read.all")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new LogDocumentViewCommand(id, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()), cancellationToken);
        var url = await mediator.Send(new GetDocumentDownloadUrlQuery(id, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()), cancellationToken);
        return Redirect(url);
    }

    /// <summary>ES-backed, OCR text included. AC-DOC3.</summary>
    [HttpGet("search")]
    [RequirePermission("documents.read.all")]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] Guid? matterId, [FromQuery] string? type, [FromQuery] string? tag, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<DocumentSearchHit>>.Of(await mediator.Send(new SearchDocumentsQuery(q, matterId, type, tag, from, to), cancellationToken)));

    [HttpPost("{id:guid}/share-links")]
    [RequirePermission("documents.manage.all")]
    public async Task<IActionResult> CreateShareLink(Guid id, [FromBody] CreateShareLinkRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<CreateShareLinkResult>.Of(await mediator.Send(new CreateShareLinkCommand(id, request.ExpiresAt, request.Password, request.MaxDownloads, request.Watermark), cancellationToken)));

    [HttpPost("{id:guid}/signature")]
    [RequirePermission("documents.manage.all")]
    public async Task<IActionResult> SendForSignature(Guid id, [FromBody] SendForSignatureRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<SignatureEnvelopeDto>.Of(await mediator.Send(new SendForSignatureCommand(id, request.Provider, request.Signers), cancellationToken)));

    [HttpPost("bulk")]
    [RequirePermission("documents.manage.all")]
    public async Task<IActionResult> Bulk([FromBody] BulkDocumentActionRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new BulkDocumentActionCommand(request.Action, request.Ids, request.TargetFolderId, request.TagNames), cancellationToken);
        return NoContent();
    }
}

/// <summary>Distinct top-level resource per PRD §17: DELETE /api/v1/share-links/{id}.</summary>
[ApiController]
[Route("api/v1/share-links")]
public sealed class ShareLinksController(IMediator mediator) : ControllerBase
{
    [HttpDelete("{id:guid}")]
    [RequirePermission("documents.manage.all")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RevokeShareLinkCommand(id), cancellationToken);
        return NoContent();
    }
}

public sealed record UploadDocumentForm(Guid? FolderId, Guid? MatterId, Guid? ClientId, Guid? CaseId, string Title, string DocType, string Confidentiality, string? Tags);

public sealed record UpdateDocumentMetadataRequest(string Title, string DocType, string Confidentiality, Guid? FolderId);

public sealed record CreateShareLinkRequest(DateTimeOffset ExpiresAt, string? Password, int? MaxDownloads, bool Watermark);

public sealed record SendForSignatureRequest(string Provider, IReadOnlyList<SignerInput> Signers);

public sealed record BulkDocumentActionRequest(string Action, IReadOnlyList<Guid> Ids, Guid? TargetFolderId, IReadOnlyList<string>? TagNames);
