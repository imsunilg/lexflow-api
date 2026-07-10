namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 17 User Flow #5: published-to-portal document list/download + upload-to-firm.
/// Uploads land in the matter's "Client Uploads" folder (created on first upload), are
/// AV-scanned via the same pipeline staff uploads use (IDocumentService.CreateAsync), and
/// notify the matter's responsible lawyer (AC-P4).
/// </summary>
public interface IPortalDocumentService
{
    /// <summary>Only documents with PortalPublished = true, scoped to clientId (and matterId if given).</summary>
    Task<IReadOnlyList<PortalDocumentDto>> GetDocumentsAsync(Guid tenantId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, Guid? matterId, CancellationToken cancellationToken = default);

    Task<string> GetDownloadUrlAsync(Guid tenantId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, Guid documentId, string? ip, string? userAgent, CancellationToken cancellationToken = default);

    /// <summary>Validation: uploads &lt;= 25 MB, types restricted (enforced the same way as staff uploads).</summary>
    Task<PortalDocumentDto> UploadAsync(Guid tenantId, Guid clientPortalUserId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, Guid matterId, byte[] fileContent, string fileName, string? mime, string title, CancellationToken cancellationToken = default);
}

public sealed record PortalDocumentDto(Guid Id, string Title, string DocType, Guid? MatterId, DateTimeOffset CreatedAt);
