namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 13 orchestration: "POST /api/v1/reports/{key}/run {params} -&gt; jobId for heavy, inline
/// for light." Error Handling: "Run timeout 120 s -&gt; auto-converts to async job with notify."
/// Every run — inline or job — is persisted as an rpt.report_runs row (Security: "export audited
/// with report key + row count"). <see cref="ExecuteQueuedRunAsync"/> is the Hangfire job entry
/// point invoked by LexFlow.Workers when a run is converted to async.
/// </summary>
public interface IReportRunService
{
    Task<ReportRunOutcome> RunStandardAsync(Guid tenantId, Guid userId, string reportKey, ReportRunParams reportParams, CancellationToken cancellationToken = default);

    Task<ReportRunOutcome> RunCustomAsync(Guid tenantId, Guid userId, Guid definitionId, CancellationToken cancellationToken = default);

    Task<ReportRunDto?> GetRunAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken = default);

    /// <summary>Hangfire-invoked: re-resolves the runner's scope, executes the report, and completes/fails the run row. Also sends the "run complete" notification (Error Handling: "auto-converts to async job with notify").</summary>
    Task ExecuteQueuedRunAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken = default);

    Task<ExportedFile?> ExportAsync(Guid tenantId, Guid runId, string format, CancellationToken cancellationToken = default);
}

public sealed record ReportRunOutcome(Guid RunId, string Status, ReportResult? InlineResult);

public sealed record ReportRunDto(Guid Id, string? ReportKey, Guid? ReportDefinitionId, string Status, int? RowCount, string? ErrorMessage, DateTimeOffset RequestedAt, DateTimeOffset? CompletedAt);

public sealed record ExportedFile(byte[] Content, string ContentType, string FileName);
