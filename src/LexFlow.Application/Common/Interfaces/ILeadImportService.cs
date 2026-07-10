namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 2 CSV/XLSX import pipeline (AC-L5: "10k-row import completes ≤ 3 min with
/// progress bar and error CSV"). Uploads the source file to blob storage, writes a
/// crm.lead_import_batches row, and enqueues a Hangfire background job
/// (<c>LeadImportJob</c> in LexFlow.Infrastructure) that does the actual per-row work —
/// this interface only covers the synchronous "kick it off / check on it" half.
/// </summary>
public interface ILeadImportService
{
    Task<LeadImportBatchDto> EnqueueImportAsync(Guid tenantId, Guid? actorId, string fileName, byte[] fileContent, CancellationToken cancellationToken = default);

    Task<LeadImportBatchDto?> GetBatchAsync(Guid tenantId, Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>Invoked by the Hangfire job itself — not part of the public API surface.</summary>
    Task RunImportAsync(Guid tenantId, Guid batchId, CancellationToken cancellationToken = default);
}

public sealed record LeadImportBatchDto(
    Guid Id,
    string FileName,
    string Status,
    int TotalRows,
    int SuccessCount,
    int ErrorCount,
    string? ErrorFileBlobPath,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason);
