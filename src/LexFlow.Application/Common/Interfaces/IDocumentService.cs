namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 7 (Document Management) — PRD §17 API list. AC-DOC3: every read path here
/// enforces the confidentiality gate (Privileged requires document.privileged.read) —
/// gated reads return null/throw NotFound rather than a 403, so a privileged doc's
/// existence isn't leaked to a caller who can't see it either.
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Full upload pipeline: AV scan (blocks on a positive hit) → blob store → Document +
    /// DocumentVersion + outbox row in one transaction → enqueues the async text-extraction
    /// job. Returns before extraction/indexing complete (those are async).
    /// </summary>
    Task<DocumentDto> CreateAsync(Guid tenantId, Guid? actorId, CreateDocumentInput input, byte[] fileContent, string fileName, string? mime, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default);

    Task<DocumentDto?> GetByIdAsync(Guid tenantId, Guid documentId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentDto>> GetAllAsync(Guid tenantId, DocumentFilter filter, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default);

    Task<DocumentDto> UpdateMetadataAsync(Guid tenantId, Guid documentId, string title, string docType, string confidentiality, Guid? folderId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, Guid documentId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default);

    Task<DocumentVersionDto> AddVersionAsync(Guid tenantId, Guid? actorId, Guid documentId, byte[] fileContent, string fileName, string? mime, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentVersionDto>> GetVersionsAsync(Guid tenantId, Guid documentId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default);

    /// <summary>AC-DOC2: "current pointer switchable with permission" — restoring copies the old version's blob forward as a brand-new version; history is never rewritten.</summary>
    Task<DocumentVersionDto> RestoreVersionAsync(Guid tenantId, Guid? actorId, Guid documentId, int versionNo, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default);

    /// <summary>Logs a document_activity row (Security Rules: "every view/download/print writes document_activity") and returns a time-limited blob URL.</summary>
    Task<string> GetDownloadUrlAsync(Guid tenantId, Guid? actorId, Guid documentId, string? ip, string? userAgent, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default);

    Task LogActivityAsync(Guid tenantId, Guid? actorId, Guid documentId, string action, string? ip, string? userAgent, CancellationToken cancellationToken = default);

    Task BulkActionAsync(Guid tenantId, Guid? actorId, string action, IReadOnlyList<Guid> documentIds, Guid? targetFolderId, IReadOnlyList<string>? tagNames, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default);
}

public sealed record CreateDocumentInput(Guid? FolderId, Guid? MatterId, Guid? ClientId, Guid? CaseId, string Title, string DocType, string Confidentiality, IReadOnlyList<string>? TagNames);

public sealed record DocumentFilter(Guid? MatterId, Guid? FolderId, string? DocType, string? Query);

public sealed record DocumentVersionDto(Guid Id, Guid DocumentId, int VersionNo, long SizeBytes, string? Mime, string HashSha256, string OcrStatus, bool TextExtracted, Guid? UploadedBy, DateTimeOffset CreatedAt);

public sealed record DocumentDto(
    Guid Id,
    string Title,
    string DocType,
    string Confidentiality,
    Guid? FolderId,
    Guid? MatterId,
    Guid? ClientId,
    Guid? CaseId,
    Guid? CurrentVersionId,
    bool PortalPublished,
    DateTimeOffset CreatedAt);
