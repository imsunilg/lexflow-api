namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 1 dashboard — GET /api/v1/dashboard/layout (+PUT) and the
/// GET /api/v1/dashboard/widgets/* feed endpoints. Visible to any authenticated
/// staff user regardless of module permissions (the dashboard only ever
/// surfaces summary counts, never a full record), so the controller gates on
/// [Authorize] alone rather than [RequirePermission].
/// </summary>
public interface IDashboardService
{
    Task<DashboardLayoutDto> GetLayoutAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<DashboardLayoutDto> SaveLayoutAsync(Guid tenantId, Guid userId, DashboardLayoutDto layout, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HearingTodayItemDto>> GetHearingsTodayAsync(Guid tenantId, DateOnly date, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskPendingItemDto>> GetTasksPendingAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeadlineItemDto>> GetDeadlinesAsync(Guid tenantId, int days, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActivityItemDto>> GetActivityAsync(Guid tenantId, Guid userId, int limit, CancellationToken cancellationToken = default);

    /// <summary>"Clear All" — advances the user's own cursor, never deletes from audit.audit_events.</summary>
    Task ClearActivityAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<CaseStatsSummaryDto> GetCaseStatsAsync(Guid tenantId, DateOnly start, DateOnly end, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LawyerPerformanceItemDto>> GetLawyerPerformanceAsync(Guid tenantId, DateOnly start, DateOnly end, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MatterSummaryItemDto>> GetMatterSummaryAsync(Guid tenantId, DateOnly start, DateOnly end, CancellationToken cancellationToken = default);

    Task<ClientSummaryWidgetDataDto> GetClientSummaryAsync(Guid tenantId, DateOnly start, DateOnly end, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeadPipelineStageDto>> GetLeadPipelineAsync(Guid tenantId, DateOnly start, DateOnly end, CancellationToken cancellationToken = default);
}

public sealed record DashboardWidgetLayoutEntryDto(string WidgetId, string Size, int Order, bool Visible);

public sealed record DashboardLayoutDto(IReadOnlyList<DashboardWidgetLayoutEntryDto> Widgets);

public sealed record HearingTodayItemDto(Guid Id, string? Time, string CourtName, string CaseNumber, string MatterTitle, string ClientName, string? AssignedLawyerName);

public sealed record TaskPendingItemDto(Guid Id, string Title, string DueDate, string Bucket, string? MatterTitle);

public sealed record DeadlineItemDto(Guid Id, string Title, string DueDate, string Severity, string? MatterTitle);

public sealed record ActivityItemDto(Guid Id, string Message, string ActorName, DateTimeOffset OccurredAt);

public sealed record CaseStatsSummaryDto(int Open, int Closed, int Won, int Lost, int Settled, IReadOnlyList<CaseStatsStageDto> ByStage);

public sealed record CaseStatsStageDto(string Stage, int Count);

public sealed record LawyerPerformanceItemDto(Guid LawyerId, string LawyerName, decimal BillableHours, decimal UtilizationPct, decimal RealizationPct);

public sealed record MatterSummaryItemDto(string Status, string PracticeArea, int Count);

public sealed record ClientSummaryWidgetDataDto(int NewThisMonth, int Active, int AtRisk);

public sealed record LeadPipelineStageDto(string Stage, int Count);
