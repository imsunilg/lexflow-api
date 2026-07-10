using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to rpt.report_schedules (lexflow-database Scripts/17_Reporting_StarSchema/ReportSchedules).
/// Module 13: "schedule (email PDF/XLSX daily/weekly/monthly to recipients)". AC-R3: "scheduled
/// report arrives within 15 min of schedule with correct attachment."
/// </summary>
public sealed class ReportSchedule : AuditableEntity
{
    private ReportSchedule()
    {
    }

    public ReportSchedule(Guid tenantId, string? reportKey, Guid? reportDefinitionId, string frequency, string format, string paramsJson, string recipientsJson, DateTimeOffset nextRunAt)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ReportKey = reportKey;
        ReportDefinitionId = reportDefinitionId;
        Frequency = frequency;
        Format = format;
        ParamsJson = paramsJson;
        RecipientsJson = recipientsJson;
        NextRunAt = nextRunAt;
        IsActive = true;
    }

    public string? ReportKey { get; private set; }
    public Guid? ReportDefinitionId { get; private set; }
    public string Frequency { get; private set; } = "daily";
    public string Format { get; private set; } = "pdf";
    public string ParamsJson { get; private set; } = "{}";
    public string RecipientsJson { get; private set; } = "[]";
    public DateTimeOffset? NextRunAt { get; private set; }
    public DateTimeOffset? LastRunAt { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void RecordRun(DateTimeOffset ranAt, DateTimeOffset nextRunAt)
    {
        LastRunAt = ranAt;
        NextRunAt = nextRunAt;
    }

    public void Deactivate() => IsActive = false;
}
