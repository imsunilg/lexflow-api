using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Dms;

/// <summary>See IDocumentProcessingJobs. AC-DOC1: a 10-page scan must be searchable by inner text ≤ 60s after upload — this job is the "extract" half of that budget.</summary>
public sealed class DocumentProcessingJobs(LexFlowDbContext db, IBlobStorageService blobStorage, ITextExtractionService textExtractionService) : IDocumentProcessingJobs
{
    private const string Container = "documents";

    public async Task ExtractAndReindexAsync(Guid tenantId, Guid documentId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var version = await db.DocumentVersions.SingleOrDefaultAsync(v => v.TenantId == tenantId && v.Id == versionId, cancellationToken);
        if (version is null)
        {
            return;
        }

        version.SetOcrStatus("Processing");
        await db.SaveChangesAsync(cancellationToken);

        var content = await blobStorage.DownloadAsync(Container, version.BlobPath, cancellationToken);
        var result = await textExtractionService.ExtractAsync(content, version.Mime, cancellationToken);

        if (result.Success)
        {
            if (!string.IsNullOrEmpty(result.Text))
            {
                // Extracted text is stored as a companion blob rather than a new Postgres
                // column — dms.document_versions has no text column in the DB-5 schema, and
                // full-text content belongs in Elasticsearch (which the outbox row below
                // triggers), not duplicated in the relational store.
                await blobStorage.UploadAsync(Container, $"{version.BlobPath}.txt", System.Text.Encoding.UTF8.GetBytes(result.Text), "text/plain", cancellationToken);
                version.MarkTextExtracted();
            }

            version.SetOcrStatus(result.OcrStatus);
        }
        else
        {
            // Error Handling: "OCR failure → status OcrFailed, searchable by metadata only, retry button."
            version.SetOcrStatus("Failed");
        }

        var outbox = new DocumentIndexOutbox(tenantId, documentId, versionId, "Index");
        await db.DocumentIndexOutbox.AddAsync(outbox, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
