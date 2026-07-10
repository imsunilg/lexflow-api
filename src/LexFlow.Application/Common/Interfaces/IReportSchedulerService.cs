namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 13: "schedule (email PDF/XLSX daily/weekly/monthly to recipients)." AC-R3: "scheduled
/// report arrives within 15 min of schedule with correct attachment." Validation: "schedule
/// recipients must be firm users or verified emails."
/// </summary>
public interface IReportSchedulerService
{
    Task<ReportScheduleDto> CreateAsync(Guid tenantId, Guid ownerId, ReportScheduleInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportScheduleDto>> GetForOwnerAsync(Guid tenantId, Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>Hangfire-invoked (recurring, at least every 15 min per AC-R3): runs every due schedule, renders the export, and emails it to recipients.</summary>
    Task RunDueSchedulesAsync(CancellationToken cancellationToken = default);
}

public sealed record ReportScheduleInput(string? ReportKey, Guid? ReportDefinitionId, string Frequency, string Format, ReportRunParams? Params, IReadOnlyList<ReportScheduleRecipient> Recipients);

public sealed record ReportScheduleRecipient(Guid? UserId, string? Email);

public sealed record ReportScheduleDto(Guid Id, string? ReportKey, Guid? ReportDefinitionId, string Frequency, string Format, DateTimeOffset? NextRunAt, DateTimeOffset? LastRunAt, bool IsActive);
