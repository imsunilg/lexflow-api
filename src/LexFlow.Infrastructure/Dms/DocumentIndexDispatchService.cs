using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Dms;

/// <summary>
/// Consumer half of the transactional-outbox pattern for ES indexing. Registered as a
/// Hangfire recurring job (LexFlow.Workers/Program.cs) rather than a real Service
/// Bus/queue subscriber — no Service Bus namespace is provisioned anywhere in this
/// solution, so a short-interval poll-and-dispatch loop over
/// dms.document_index_outbox stands in for "background dispatcher publishes to
/// Service Bus/queue, an indexer worker consumes and calls ES": the outbox table
/// still guarantees at-least-once delivery (a row written in the same transaction as
/// the document insert is never lost even if this dispatcher is down for a while),
/// which is the actual point of the pattern.
/// </summary>
public sealed class DocumentIndexDispatchService(LexFlowDbContext db, IDocumentIndexer indexer, IBlobStorageService blobStorage)
{
    private const int BatchSize = 50;
    private const string Container = "documents";

    public async Task DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await db.DocumentIndexOutbox
            .Where(o => o.Status == "Pending")
            .OrderBy(o => o.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var entry in pending)
        {
            entry.MarkDispatched();
            await db.SaveChangesAsync(cancellationToken);

            try
            {
                if (entry.Operation == "Delete")
                {
                    await indexer.DeleteAsync(entry.TenantId, entry.DocumentId, cancellationToken);
                }
                else
                {
                    await IndexOneAsync(entry, cancellationToken);
                }

                entry.MarkDone();
            }
            catch (Exception ex)
            {
                entry.MarkFailed(ex.Message);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task IndexOneAsync(DocumentIndexOutbox entry, CancellationToken cancellationToken)
    {
        var document = await db.Documents.SingleOrDefaultAsync(d => d.Id == entry.DocumentId, cancellationToken);
        if (document is null)
        {
            return;
        }

        string? extractedText = null;
        var currentVersion = document.CurrentVersionId.HasValue
            ? await db.DocumentVersions.SingleOrDefaultAsync(v => v.Id == document.CurrentVersionId, cancellationToken)
            : null;

        if (currentVersion is { TextExtracted: true })
        {
            try
            {
                var textBytes = await blobStorage.DownloadAsync(Container, $"{currentVersion.BlobPath}.txt", cancellationToken);
                extractedText = System.Text.Encoding.UTF8.GetString(textBytes);
            }
            catch
            {
                // Companion text blob missing/unreadable — index metadata-only rather than fail the whole dispatch cycle.
            }
        }

        var tags = await (
            from dt in db.DocumentTags
            join t in db.Tags on dt.TagId equals t.Id
            where dt.DocumentId == document.Id
            select t.Name).ToListAsync(cancellationToken);

        var payload = new DocumentIndexPayload(document.Title, document.DocType, document.Confidentiality, document.MatterId, document.ClientId, document.CaseId, extractedText, tags);
        await indexer.IndexAsync(entry.TenantId, entry.DocumentId, payload, cancellationToken);
    }
}
