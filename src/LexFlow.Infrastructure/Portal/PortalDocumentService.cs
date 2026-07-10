using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Portal;

/// <summary>
/// Module 17 User Flow #5: published-to-portal documents (download) + upload-to-firm
/// (client uploads land in the matter's "Client Uploads" folder). Uploads reuse
/// IDocumentService.CreateAsync verbatim — the exact same AV-scan -> blob-store ->
/// Document+DocumentVersion pipeline staff uploads go through (AC-P4), not a separate,
/// weaker path.
/// </summary>
public sealed class PortalDocumentService(LexFlowDbContext db, IDocumentService documentService, INotificationService notificationService) : IPortalDocumentService
{
    private const long MaxUploadBytes = 25 * 1024 * 1024;
    private const string ClientUploadsFolderName = "Client Uploads";

    public async Task<IReadOnlyList<PortalDocumentDto>> GetDocumentsAsync(Guid tenantId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, Guid? matterId, CancellationToken cancellationToken = default)
    {
        if (matterId is { } id)
        {
            await EnsureMatterOwnedByClientAsync(tenantId, clientId, visibleMatterIds, id, cancellationToken);
        }

        var query = db.Documents.Where(d => d.TenantId == tenantId && d.ClientId == clientId && d.PortalPublished && d.Confidentiality != "Privileged");
        if (matterId is { } m)
        {
            query = query.Where(d => d.MatterId == m);
        }

        var documents = await query.OrderByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);
        documents = documents.Where(d => d.MatterId is null || visibleMatterIds is null || visibleMatterIds.Contains(d.MatterId.Value)).ToList();

        return documents.Select(d => new PortalDocumentDto(d.Id, d.Title, d.DocType, d.MatterId, d.CreatedAt)).ToList();
    }

    public async Task<string> GetDownloadUrlAsync(Guid tenantId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, Guid documentId, string? ip, string? userAgent, CancellationToken cancellationToken = default)
    {
        var document = await db.Documents.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.Id == documentId, cancellationToken);
        if (document is null || document.ClientId != clientId || !document.PortalPublished || document.Confidentiality == "Privileged"
            || (document.MatterId is { } matterId && visibleMatterIds is not null && !visibleMatterIds.Contains(matterId)))
        {
            throw new NotFoundException(nameof(Document), documentId);
        }

        await db.PortalActivityLog.AddAsync(new PortalActivityLog(tenantId, clientId, "document.download", nameof(Document), documentId, ip, userAgent), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return await documentService.GetDownloadUrlAsync(tenantId, actorId: null, documentId, ip, userAgent, callerPermissions: [], cancellationToken);
    }

    public async Task<PortalDocumentDto> UploadAsync(Guid tenantId, Guid clientPortalUserId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, Guid matterId, byte[] fileContent, string fileName, string? mime, string title, CancellationToken cancellationToken = default)
    {
        var matter = await EnsureMatterOwnedByClientAsync(tenantId, clientId, visibleMatterIds, matterId, cancellationToken);

        if (fileContent.LongLength > MaxUploadBytes)
        {
            throw new DomainRuleException("UPLOAD_TOO_LARGE", $"Uploads are limited to {MaxUploadBytes / (1024 * 1024)} MB.");
        }

        var folder = await db.Folders.SingleOrDefaultAsync(f => f.TenantId == tenantId && f.MatterId == matterId && f.Name == ClientUploadsFolderName, cancellationToken);
        if (folder is null)
        {
            folder = new Folder(tenantId, ClientUploadsFolderName, null, matterId, null);
            await db.Folders.AddAsync(folder, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        var input = new CreateDocumentInput(folder.Id, matterId, clientId, null, title, "Client Upload", "Normal", null);
        var dto = await documentService.CreateAsync(tenantId, actorId: null, input, fileContent, fileName, mime, callerPermissions: [], cancellationToken);

        await db.PortalActivityLog.AddAsync(new PortalActivityLog(tenantId, clientPortalUserId, "document.upload", nameof(Document), dto.Id, null, null), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (matter.ResponsibleLawyerId is { } lawyerId)
        {
            await notificationService.NotifyAsync(tenantId, lawyerId, new NotifyRequest(
                "portal.document_uploaded", "Client uploaded a document", $"{title} was uploaded to {matter.Number} via the client portal.", null, ["Email", "InApp"]), cancellationToken);
        }

        return new PortalDocumentDto(dto.Id, dto.Title, dto.DocType, dto.MatterId, dto.CreatedAt);
    }

    private async Task<Matter> EnsureMatterOwnedByClientAsync(Guid tenantId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, Guid matterId, CancellationToken cancellationToken)
    {
        var matter = await db.Matters.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Id == matterId, cancellationToken);
        if (matter is null || matter.ClientId != clientId || (visibleMatterIds is not null && !visibleMatterIds.Contains(matter.Id)))
        {
            throw new NotFoundException(nameof(Matter), matterId);
        }

        return matter;
    }
}
