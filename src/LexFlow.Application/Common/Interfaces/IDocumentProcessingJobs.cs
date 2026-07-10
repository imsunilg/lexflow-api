namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// The Hangfire job body enqueued right after a document/version is stored (see
/// DocumentService.EnqueueTextExtraction). Runs text extraction and, on success, writes
/// a fresh DocumentIndexOutbox row so the (separately scheduled) outbox dispatcher
/// re-indexes the document with its now-available extracted text — see
/// DocumentIndexDispatchService for the consumer half of the outbox pattern.
/// </summary>
public interface IDocumentProcessingJobs
{
    Task ExtractAndReindexAsync(Guid tenantId, Guid documentId, Guid versionId, CancellationToken cancellationToken = default);
}
