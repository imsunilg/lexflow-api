using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to crm.lead_import_batches (lexflow-database
/// Scripts/03_CRM/LeadImportBatches, additive migration). Tracks one CSV/XLSX
/// import job (Module 2 "import wizard" / AC-L5), processed by a Hangfire
/// background job (<c>LeadImportJob</c> in LexFlow.Infrastructure).
/// </summary>
public sealed class LeadImportBatch : AuditableEntity
{
    private LeadImportBatch()
    {
    }

    public LeadImportBatch(Guid tenantId, string fileName, string sourceBlobPath)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        FileName = fileName;
        SourceBlobPath = sourceBlobPath;
        Status = "Pending";
    }

    public string FileName { get; private set; } = null!;
    public string SourceBlobPath { get; private set; } = null!;
    public string Status { get; private set; } = "Pending";
    public int TotalRows { get; private set; }
    public int SuccessCount { get; private set; }
    public int ErrorCount { get; private set; }
    public string? ErrorFileBlobPath { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }

    public void SetSourceBlobPath(string sourceBlobPath) => SourceBlobPath = sourceBlobPath;

    public void Start() => (Status, StartedAt) = ("Running", DateTimeOffset.UtcNow);

    public void Complete(int totalRows, int successCount, int errorCount, string? errorFileBlobPath)
    {
        Status = "Completed";
        TotalRows = totalRows;
        SuccessCount = successCount;
        ErrorCount = errorCount;
        ErrorFileBlobPath = errorFileBlobPath;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string reason)
    {
        Status = "Failed";
        FailureReason = reason;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
