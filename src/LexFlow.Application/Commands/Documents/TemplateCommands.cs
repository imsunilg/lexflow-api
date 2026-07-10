using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Documents;

/// <summary>POST /api/v1/documents/templates (multipart docx + field list).</summary>
public sealed record CreateDocumentTemplateCommand(string Name, string? Category, byte[] DocxContent, IReadOnlyList<MergeFieldInput> Fields) : IRequest<DocumentTemplateDto>;

public sealed class CreateDocumentTemplateCommandHandler(IDocumentTemplateService templateService, ICurrentUserService currentUser) : IRequestHandler<CreateDocumentTemplateCommand, DocumentTemplateDto>
{
    public Task<DocumentTemplateDto> Handle(CreateDocumentTemplateCommand request, CancellationToken cancellationToken)
        => templateService.CreateAsync(currentUser.TenantId!.Value, currentUser.UserId, request.Name, request.Category, request.DocxContent, request.Fields, cancellationToken);
}

/// <summary>POST /api/v1/documents/templates/{id}/generate {matterId, overrides}. AC-DOC4.</summary>
public sealed record GenerateFromTemplateCommand(Guid TemplateId, Guid MatterId, IReadOnlyDictionary<string, string>? Overrides) : IRequest<DocumentDto>;

public sealed class GenerateFromTemplateCommandHandler(IDocumentTemplateService templateService, ICurrentUserService currentUser) : IRequestHandler<GenerateFromTemplateCommand, DocumentDto>
{
    public Task<DocumentDto> Handle(GenerateFromTemplateCommand request, CancellationToken cancellationToken)
        => templateService.GenerateAsync(currentUser.TenantId!.Value, currentUser.UserId, request.TemplateId, request.MatterId, request.Overrides, cancellationToken);
}
