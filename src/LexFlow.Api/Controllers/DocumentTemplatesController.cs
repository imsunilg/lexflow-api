using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Documents;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Documents;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 7 template library + OpenXML merge — PRD §17 (POST /api/v1/documents/templates/{id}/generate).</summary>
[ApiController]
[Route("api/v1/documents/templates")]
public sealed class DocumentTemplatesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission("documents.manage.all")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Create([FromForm] CreateTemplateForm form, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        var fields = (form.Fields ?? [])
            .Select(f =>
            {
                var parts = f.Split(':', 3);
                return new MergeFieldInput(parts[0], parts.Length > 1 ? parts[1] : null, parts.Length > 2 && bool.TryParse(parts[2], out var required) && required);
            })
            .ToList();

        var command = new CreateDocumentTemplateCommand(form.Name, form.Category, stream.ToArray(), fields);
        return Ok(ApiResponse<DocumentTemplateDto>.Of(await mediator.Send(command, cancellationToken)));
    }

    [HttpGet]
    [RequirePermission("documents.read.all")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<DocumentTemplateDto>>.Of(await mediator.Send(new GetDocumentTemplatesQuery(), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission("documents.read.all")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<DocumentTemplateDto>.Of(await mediator.Send(new GetDocumentTemplateQuery(id), cancellationToken)));

    /// <summary>AC-DOC4.</summary>
    [HttpPost("{id:guid}/generate")]
    [RequirePermission("documents.manage.all")]
    public async Task<IActionResult> Generate(Guid id, [FromBody] GenerateFromTemplateRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<DocumentDto>.Of(await mediator.Send(new GenerateFromTemplateCommand(id, request.MatterId, request.Overrides), cancellationToken)));
}

public sealed record CreateTemplateForm(string Name, string? Category, string[]? Fields);

public sealed record GenerateFromTemplateRequest(Guid MatterId, Dictionary<string, string>? Overrides);
