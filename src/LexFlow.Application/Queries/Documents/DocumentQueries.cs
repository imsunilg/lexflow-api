using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Documents;

public sealed record GetDocumentQuery(Guid DocumentId) : IRequest<DocumentDto>;

public sealed class GetDocumentQueryHandler(IDocumentService documentService, ICurrentUserService currentUser) : IRequestHandler<GetDocumentQuery, DocumentDto>
{
    public async Task<DocumentDto> Handle(GetDocumentQuery request, CancellationToken cancellationToken)
        => await documentService.GetByIdAsync(currentUser.TenantId!.Value, request.DocumentId, currentUser.Permissions, cancellationToken)
           ?? throw new NotFoundException("Document", request.DocumentId);
}

public sealed record GetDocumentsQuery(Guid? MatterId, Guid? FolderId, string? DocType, string? Query) : IRequest<IReadOnlyList<DocumentDto>>;

public sealed class GetDocumentsQueryHandler(IDocumentService documentService, ICurrentUserService currentUser) : IRequestHandler<GetDocumentsQuery, IReadOnlyList<DocumentDto>>
{
    public Task<IReadOnlyList<DocumentDto>> Handle(GetDocumentsQuery request, CancellationToken cancellationToken)
        => documentService.GetAllAsync(currentUser.TenantId!.Value, new DocumentFilter(request.MatterId, request.FolderId, request.DocType, request.Query), currentUser.Permissions, cancellationToken);
}

public sealed record GetDocumentVersionsQuery(Guid DocumentId) : IRequest<IReadOnlyList<DocumentVersionDto>>;

public sealed class GetDocumentVersionsQueryHandler(IDocumentService documentService, ICurrentUserService currentUser) : IRequestHandler<GetDocumentVersionsQuery, IReadOnlyList<DocumentVersionDto>>
{
    public Task<IReadOnlyList<DocumentVersionDto>> Handle(GetDocumentVersionsQuery request, CancellationToken cancellationToken)
        => documentService.GetVersionsAsync(currentUser.TenantId!.Value, request.DocumentId, currentUser.Permissions, cancellationToken);
}

/// <summary>GET /api/v1/documents/{id}/download → 302 SAS.</summary>
public sealed record GetDocumentDownloadUrlQuery(Guid DocumentId, string? Ip, string? UserAgent) : IRequest<string>;

public sealed class GetDocumentDownloadUrlQueryHandler(IDocumentService documentService, ICurrentUserService currentUser) : IRequestHandler<GetDocumentDownloadUrlQuery, string>
{
    public Task<string> Handle(GetDocumentDownloadUrlQuery request, CancellationToken cancellationToken)
        => documentService.GetDownloadUrlAsync(currentUser.TenantId!.Value, currentUser.UserId, request.DocumentId, request.Ip, request.UserAgent, currentUser.Permissions, cancellationToken);
}

/// <summary>GET /api/v1/documents/{id}/preview — logs a View activity; the actual PDF preview stream is served by the controller directly from blob storage.</summary>
public sealed record LogDocumentViewCommand(Guid DocumentId, string? Ip, string? UserAgent) : IRequest;

public sealed class LogDocumentViewCommandHandler(IDocumentService documentService, ICurrentUserService currentUser) : IRequestHandler<LogDocumentViewCommand>
{
    public async Task Handle(LogDocumentViewCommand request, CancellationToken cancellationToken)
        => await documentService.LogActivityAsync(currentUser.TenantId!.Value, currentUser.UserId, request.DocumentId, "View", request.Ip, request.UserAgent, cancellationToken);
}

/// <summary>GET /api/v1/documents/search?q=&amp;matterId=&amp;type=&amp;tag=&amp;from=&amp;to= (ES-backed). AC-DOC3.</summary>
public sealed record SearchDocumentsQuery(string? Text, Guid? MatterId, string? DocType, string? Tag, DateTimeOffset? From, DateTimeOffset? To) : IRequest<IReadOnlyList<DocumentSearchHit>>;

public sealed class SearchDocumentsQueryHandler(IDocumentIndexer indexer, ICurrentUserService currentUser) : IRequestHandler<SearchDocumentsQuery, IReadOnlyList<DocumentSearchHit>>
{
    public Task<IReadOnlyList<DocumentSearchHit>> Handle(SearchDocumentsQuery request, CancellationToken cancellationToken)
        => indexer.SearchAsync(
            currentUser.TenantId!.Value,
            new DocumentSearchQuery(request.Text, request.MatterId, request.DocType, request.Tag, request.From, request.To),
            currentUser.Permissions,
            cancellationToken);
}
