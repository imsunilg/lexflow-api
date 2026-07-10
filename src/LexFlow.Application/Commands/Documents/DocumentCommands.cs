using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Documents;

/// <summary>POST /api/v1/documents (multipart, folderId, metadata). Full AV→blob→outbox pipeline — see IDocumentService.CreateAsync.</summary>
public sealed record CreateDocumentCommand(
    Guid? FolderId, Guid? MatterId, Guid? ClientId, Guid? CaseId, string Title, string DocType, string Confidentiality, IReadOnlyList<string>? TagNames,
    byte[] FileContent, string FileName, string? Mime) : IRequest<DocumentDto>;

public sealed class CreateDocumentCommandHandler(IDocumentService documentService, ICurrentUserService currentUser) : IRequestHandler<CreateDocumentCommand, DocumentDto>
{
    public Task<DocumentDto> Handle(CreateDocumentCommand request, CancellationToken cancellationToken)
        => documentService.CreateAsync(
            currentUser.TenantId!.Value, currentUser.UserId,
            new CreateDocumentInput(request.FolderId, request.MatterId, request.ClientId, request.CaseId, request.Title, request.DocType, request.Confidentiality, request.TagNames),
            request.FileContent, request.FileName, request.Mime, currentUser.Permissions, cancellationToken);
}

/// <summary>PATCH /api/v1/documents/{id}.</summary>
public sealed record UpdateDocumentMetadataCommand(Guid DocumentId, string Title, string DocType, string Confidentiality, Guid? FolderId) : IRequest<DocumentDto>;

public sealed class UpdateDocumentMetadataCommandHandler(IDocumentService documentService, ICurrentUserService currentUser) : IRequestHandler<UpdateDocumentMetadataCommand, DocumentDto>
{
    public Task<DocumentDto> Handle(UpdateDocumentMetadataCommand request, CancellationToken cancellationToken)
        => documentService.UpdateMetadataAsync(currentUser.TenantId!.Value, request.DocumentId, request.Title, request.DocType, request.Confidentiality, request.FolderId, currentUser.Permissions, cancellationToken);
}

/// <summary>DELETE /api/v1/documents/{id}.</summary>
public sealed record DeleteDocumentCommand(Guid DocumentId) : IRequest;

public sealed class DeleteDocumentCommandHandler(IDocumentService documentService, ICurrentUserService currentUser) : IRequestHandler<DeleteDocumentCommand>
{
    public async Task Handle(DeleteDocumentCommand request, CancellationToken cancellationToken)
        => await documentService.DeleteAsync(currentUser.TenantId!.Value, request.DocumentId, currentUser.Permissions, cancellationToken);
}

/// <summary>POST /api/v1/documents/{id}/versions.</summary>
public sealed record AddDocumentVersionCommand(Guid DocumentId, byte[] FileContent, string FileName, string? Mime) : IRequest<DocumentVersionDto>;

public sealed class AddDocumentVersionCommandHandler(IDocumentService documentService, ICurrentUserService currentUser) : IRequestHandler<AddDocumentVersionCommand, DocumentVersionDto>
{
    public Task<DocumentVersionDto> Handle(AddDocumentVersionCommand request, CancellationToken cancellationToken)
        => documentService.AddVersionAsync(currentUser.TenantId!.Value, currentUser.UserId, request.DocumentId, request.FileContent, request.FileName, request.Mime, currentUser.Permissions, cancellationToken);
}

/// <summary>POST /api/v1/documents/{id}/versions/{v}/restore. AC-DOC2.</summary>
public sealed record RestoreDocumentVersionCommand(Guid DocumentId, int VersionNo) : IRequest<DocumentVersionDto>;

public sealed class RestoreDocumentVersionCommandHandler(IDocumentService documentService, ICurrentUserService currentUser) : IRequestHandler<RestoreDocumentVersionCommand, DocumentVersionDto>
{
    public Task<DocumentVersionDto> Handle(RestoreDocumentVersionCommand request, CancellationToken cancellationToken)
        => documentService.RestoreVersionAsync(currentUser.TenantId!.Value, currentUser.UserId, request.DocumentId, request.VersionNo, currentUser.Permissions, cancellationToken);
}

/// <summary>POST /api/v1/documents/bulk {action, ids[]}.</summary>
public sealed record BulkDocumentActionCommand(string Action, IReadOnlyList<Guid> DocumentIds, Guid? TargetFolderId, IReadOnlyList<string>? TagNames) : IRequest;

public sealed class BulkDocumentActionCommandHandler(IDocumentService documentService, ICurrentUserService currentUser) : IRequestHandler<BulkDocumentActionCommand>
{
    public async Task Handle(BulkDocumentActionCommand request, CancellationToken cancellationToken)
        => await documentService.BulkActionAsync(currentUser.TenantId!.Value, currentUser.UserId, request.Action, request.DocumentIds, request.TargetFolderId, request.TagNames, currentUser.Permissions, cancellationToken);
}
