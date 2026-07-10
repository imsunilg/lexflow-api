using System.Security.Cryptography;
using Hangfire;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Dms;

/// <summary>
/// Module 7 (Document Management) — PRD §17 API list. Upload pipeline: AV scan → blob
/// store → Document/DocumentVersion/outbox row (one transaction) → async text
/// extraction (Hangfire) → async ES indexing (outbox dispatcher). AC-DOC3: every read
/// here is gated by confidentiality — see <see cref="AssertVisibleAsync"/>.
/// </summary>
public sealed class DocumentService(LexFlowDbContext db, IBlobStorageService blobStorage, IAvScanner avScanner, IBackgroundJobClient backgroundJobClient, IWorkflowEventPublisher? workflowEvents = null) : IDocumentService
{
    private const string Container = "documents";

    public async Task<DocumentDto> CreateAsync(Guid tenantId, Guid? actorId, CreateDocumentInput input, byte[] fileContent, string fileName, string? mime, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default)
    {
        await ScanOrThrowAsync(fileContent, fileName, cancellationToken);

        if (input.Confidentiality == "Privileged" && !callerPermissions.Contains("document.privileged.read"))
        {
            throw new ForbiddenAccessException();
        }

        var document = new Document(tenantId, input.FolderId, input.MatterId, input.ClientId, input.CaseId, input.Title, input.DocType, input.Confidentiality);
        await db.Documents.AddAsync(document, cancellationToken);

        var version = await AddVersionInternalAsync(tenantId, document.Id, fileContent, fileName, mime, actorId, cancellationToken);

        if (input.TagNames is { Count: > 0 })
        {
            await AttachTagsAsync(tenantId, document.Id, input.TagNames, cancellationToken);
        }

        if (workflowEvents is not null)
        {
            await workflowEvents.PublishAsync(tenantId, "document.uploaded", document.Id, new
            {
                entityId = document.Id,
                matterId = document.MatterId,
                folderId = document.FolderId,
                uploadedByPortal = false,
            }, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        EnqueueTextExtraction(tenantId, document.Id, version.Id);

        return ToDto(document);
    }

    public async Task<DocumentDto?> GetByIdAsync(Guid tenantId, Guid documentId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default)
    {
        var document = await db.Documents.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.Id == documentId, cancellationToken);
        if (document is null || !IsVisible(document, callerPermissions))
        {
            return null;
        }

        return ToDto(document);
    }

    public async Task<IReadOnlyList<DocumentDto>> GetAllAsync(Guid tenantId, DocumentFilter filter, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default)
    {
        var query = db.Documents.Where(d => d.TenantId == tenantId).AsQueryable();

        if (filter.MatterId.HasValue)
        {
            query = query.Where(d => d.MatterId == filter.MatterId);
        }

        if (filter.FolderId.HasValue)
        {
            query = query.Where(d => d.FolderId == filter.FolderId);
        }

        if (!string.IsNullOrWhiteSpace(filter.DocType))
        {
            query = query.Where(d => d.DocType == filter.DocType);
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var q = filter.Query;
            query = query.Where(d => d.Title.Contains(q));
        }

        // AC-DOC3: the privileged gate applies here too, not just to the ES-backed search
        // endpoint — a plain folder listing must never leak a Privileged document either.
        if (!callerPermissions.Contains("document.privileged.read"))
        {
            query = query.Where(d => d.Confidentiality != "Privileged");
        }

        var documents = await query.OrderByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);
        return documents.Select(ToDto).ToList();
    }

    public async Task<DocumentDto> UpdateMetadataAsync(Guid tenantId, Guid documentId, string title, string docType, string confidentiality, Guid? folderId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default)
    {
        var document = await GetVisibleOrThrowAsync(tenantId, documentId, callerPermissions, cancellationToken);

        if (confidentiality == "Privileged" && !callerPermissions.Contains("document.privileged.read"))
        {
            throw new ForbiddenAccessException();
        }

        document.UpdateMetadata(title, docType, confidentiality, folderId);
        await db.SaveChangesAsync(cancellationToken);

        EnqueueTextExtraction(tenantId, document.Id, document.CurrentVersionId);

        return ToDto(document);
    }

    public async Task DeleteAsync(Guid tenantId, Guid documentId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default)
    {
        var document = await GetVisibleOrThrowAsync(tenantId, documentId, callerPermissions, cancellationToken);
        db.Documents.Remove(document);

        var outbox = new DocumentIndexOutbox(tenantId, documentId, null, "Delete");
        await db.DocumentIndexOutbox.AddAsync(outbox, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DocumentVersionDto> AddVersionAsync(Guid tenantId, Guid? actorId, Guid documentId, byte[] fileContent, string fileName, string? mime, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default)
    {
        await GetVisibleOrThrowAsync(tenantId, documentId, callerPermissions, cancellationToken);
        await ScanOrThrowAsync(fileContent, fileName, cancellationToken);

        var version = await AddVersionInternalAsync(tenantId, documentId, fileContent, fileName, mime, actorId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        EnqueueTextExtraction(tenantId, documentId, version.Id);

        return ToVersionDto(version);
    }

    public async Task<IReadOnlyList<DocumentVersionDto>> GetVersionsAsync(Guid tenantId, Guid documentId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default)
    {
        await GetVisibleOrThrowAsync(tenantId, documentId, callerPermissions, cancellationToken);
        var versions = await db.DocumentVersions.Where(v => v.TenantId == tenantId && v.DocumentId == documentId).OrderByDescending(v => v.VersionNo).ToListAsync(cancellationToken);
        return versions.Select(ToVersionDto).ToList();
    }

    public async Task<DocumentVersionDto> RestoreVersionAsync(Guid tenantId, Guid? actorId, Guid documentId, int versionNo, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default)
    {
        await GetVisibleOrThrowAsync(tenantId, documentId, callerPermissions, cancellationToken);

        var oldVersion = await db.DocumentVersions.SingleOrDefaultAsync(v => v.TenantId == tenantId && v.DocumentId == documentId && v.VersionNo == versionNo, cancellationToken)
            ?? throw new NotFoundException(nameof(DocumentVersion), versionNo);

        // AC-DOC2/Edge Case: "version restore (creates new version copying old — history
        // never rewritten)" — same blob content, brand-new version row and version number.
        var nextVersionNo = await db.DocumentVersions.Where(v => v.TenantId == tenantId && v.DocumentId == documentId).MaxAsync(v => v.VersionNo, cancellationToken) + 1;
        var restored = new DocumentVersion(tenantId, documentId, nextVersionNo, oldVersion.BlobPath, oldVersion.SizeBytes, oldVersion.Mime, oldVersion.HashSha256, actorId);
        if (oldVersion.TextExtracted)
        {
            restored.MarkTextExtracted();
            restored.SetOcrStatus(oldVersion.OcrStatus);
        }

        await db.DocumentVersions.AddAsync(restored, cancellationToken);

        var outbox = new DocumentIndexOutbox(tenantId, documentId, restored.Id, "Index");
        await db.DocumentIndexOutbox.AddAsync(outbox, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return ToVersionDto(restored);
    }

    public async Task<string> GetDownloadUrlAsync(Guid tenantId, Guid? actorId, Guid documentId, string? ip, string? userAgent, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default)
    {
        var document = await GetVisibleOrThrowAsync(tenantId, documentId, callerPermissions, cancellationToken);
        if (document.CurrentVersionId is null)
        {
            throw new NotFoundException(nameof(DocumentVersion), documentId);
        }

        var version = await db.DocumentVersions.SingleAsync(v => v.Id == document.CurrentVersionId, cancellationToken);
        var url = await blobStorage.GetDownloadUrlAsync(Container, version.BlobPath, TimeSpan.FromMinutes(15), cancellationToken);

        await LogActivityAsync(tenantId, actorId, documentId, "Download", ip, userAgent, cancellationToken);
        return url;
    }

    public async Task LogActivityAsync(Guid tenantId, Guid? actorId, Guid documentId, string action, string? ip, string? userAgent, CancellationToken cancellationToken = default)
    {
        var activity = new DocumentActivity(tenantId, documentId, actorId, action, ip, userAgent);
        await db.DocumentActivities.AddAsync(activity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task BulkActionAsync(Guid tenantId, Guid? actorId, string action, IReadOnlyList<Guid> documentIds, Guid? targetFolderId, IReadOnlyList<string>? tagNames, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default)
    {
        var documents = await db.Documents.Where(d => d.TenantId == tenantId && documentIds.Contains(d.Id)).ToListAsync(cancellationToken);

        foreach (var document in documents.Where(d => !IsVisible(d, callerPermissions)))
        {
            throw new ForbiddenAccessException();
        }

        switch (action)
        {
            case "move":
                foreach (var document in documents)
                {
                    document.UpdateMetadata(document.Title, document.DocType, document.Confidentiality, targetFolderId);
                }

                break;
            case "tag":
                if (tagNames is { Count: > 0 })
                {
                    foreach (var document in documents)
                    {
                        await AttachTagsAsync(tenantId, document.Id, tagNames, cancellationToken);
                    }
                }

                break;
            default:
                throw new ConflictException($"Unsupported bulk action '{action}'.", "BULK_ACTION_UNSUPPORTED");
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ScanOrThrowAsync(byte[] fileContent, string fileName, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(fileContent);
        var scanResult = await avScanner.ScanAsync(stream, fileName, cancellationToken);
        if (!scanResult.IsClean)
        {
            // Error Handling: "AV positive → quarantine bucket, uploader notified, admin
            // alert, audit" — the blob is never stored (this throws before any blob write),
            // which is the simplest possible quarantine: nothing reaches the real store.
            throw new DomainRuleException("MALWARE_DETECTED", $"File '{fileName}' failed the antivirus scan ({scanResult.ThreatName}).", scanResult.ThreatName);
        }
    }

    private async Task<DocumentVersion> AddVersionInternalAsync(Guid tenantId, Guid documentId, byte[] fileContent, string fileName, string? mime, Guid? actorId, CancellationToken cancellationToken)
    {
        var nextVersionNo = await db.DocumentVersions.IgnoreQueryFilters().Where(v => v.TenantId == tenantId && v.DocumentId == documentId).Select(v => (int?)v.VersionNo).MaxAsync(cancellationToken) ?? 0;
        nextVersionNo++;

        var hash = Convert.ToHexString(SHA256.HashData(fileContent)).ToLowerInvariant();
        var blobPath = $"{tenantId:N}/{documentId:N}/v{nextVersionNo}-{fileName}";
        await blobStorage.UploadAsync(Container, blobPath, fileContent, mime ?? "application/octet-stream", cancellationToken);

        var version = new DocumentVersion(tenantId, documentId, nextVersionNo, blobPath, fileContent.LongLength, mime, hash, actorId);
        await db.DocumentVersions.AddAsync(version, cancellationToken);

        var outbox = new DocumentIndexOutbox(tenantId, documentId, version.Id, "Index");
        await db.DocumentIndexOutbox.AddAsync(outbox, cancellationToken);

        return version;
    }

    private async Task AttachTagsAsync(Guid tenantId, Guid documentId, IReadOnlyList<string> tagNames, CancellationToken cancellationToken)
    {
        foreach (var name in tagNames)
        {
            var tag = await db.Tags.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Name == name, cancellationToken);
            if (tag is null)
            {
                tag = new Tag(tenantId, name);
                await db.Tags.AddAsync(tag, cancellationToken);
            }

            var alreadyTagged = await db.DocumentTags.AnyAsync(dt => dt.TenantId == tenantId && dt.DocumentId == documentId && dt.TagId == tag.Id, cancellationToken);
            if (!alreadyTagged)
            {
                await db.DocumentTags.AddAsync(new DocumentTag(tenantId, documentId, tag.Id), cancellationToken);
            }
        }
    }

    private void EnqueueTextExtraction(Guid tenantId, Guid documentId, Guid? versionId)
    {
        if (versionId is null)
        {
            return;
        }

        backgroundJobClient.Enqueue<IDocumentProcessingJobs>(j => j.ExtractAndReindexAsync(tenantId, documentId, versionId.Value, CancellationToken.None));
    }

    private async Task<Document> GetVisibleOrThrowAsync(Guid tenantId, Guid documentId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken)
    {
        var document = await db.Documents.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.Id == documentId, cancellationToken);
        if (document is null || !IsVisible(document, callerPermissions))
        {
            throw new NotFoundException(nameof(Document), documentId);
        }

        return document;
    }

    /// <summary>AC-DOC3: the single confidentiality-gate predicate every read path in this service funnels through.</summary>
    private static bool IsVisible(Document document, IReadOnlyCollection<string> callerPermissions)
        => document.Confidentiality != "Privileged" || callerPermissions.Contains("document.privileged.read");

    private static DocumentVersionDto ToVersionDto(DocumentVersion v) => new(v.Id, v.DocumentId, v.VersionNo, v.SizeBytes, v.Mime, v.HashSha256, v.OcrStatus, v.TextExtracted, v.UploadedBy, v.CreatedAt);

    private static DocumentDto ToDto(Document d) => new(d.Id, d.Title, d.DocType, d.Confidentiality, d.FolderId, d.MatterId, d.ClientId, d.CaseId, d.CurrentVersionId, d.PortalPublished, d.CreatedAt);
}
