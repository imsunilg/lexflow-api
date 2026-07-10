using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to rpt.report_runs (lexflow-database Scripts/17_Reporting_StarSchema/ReportRuns).
/// Module 13. One row per report execution, whether it resolved inline (Queued -&gt; Running -&gt;
/// Completed within the request) or was converted to a Hangfire job on the 120 s timeout
/// (Error Handling: "Run timeout 120 s -&gt; auto-converts to async job with notify").
/// </summary>
public sealed class ReportRun : AuditableEntity
{
    private ReportRun()
    {
    }

    public ReportRun(Guid tenantId, string? reportKey, Guid? reportDefinitionId, string paramsJson, Guid? requestedBy, string? format)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ReportKey = reportKey;
        ReportDefinitionId = reportDefinitionId;
        ParamsJson = paramsJson;
        RequestedBy = requestedBy;
        Format = format;
        Status = "Queued";
        RequestedAt = DateTimeOffset.UtcNow;
    }

    public string? ReportKey { get; private set; }
    public Guid? ReportDefinitionId { get; private set; }
    public Guid? ScheduleId { get; private set; }
    public string ParamsJson { get; private set; } = "{}";
    public string Status { get; private set; } = "Queued";
    public string? Format { get; private set; }
    public int? RowCount { get; private set; }
    public string? ResultBlobPath { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? RequestedBy { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public void AttachToSchedule(Guid scheduleId) => ScheduleId = scheduleId;

    public void Start() => (Status, StartedAt) = ("Running", DateTimeOffset.UtcNow);

    public void Complete(int rowCount, string? resultBlobPath)
    {
        Status = "Completed";
        RowCount = rowCount;
        ResultBlobPath = resultBlobPath;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string errorMessage)
    {
        Status = "Failed";
        ErrorMessage = errorMessage;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
