using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Dashboard;

/// <summary>GET /api/v1/dashboard/layout.</summary>
public sealed record GetDashboardLayoutQuery : IRequest<DashboardLayoutDto>;

public sealed class GetDashboardLayoutQueryHandler(IDashboardService dashboardService, ICurrentUserService currentUser) : IRequestHandler<GetDashboardLayoutQuery, DashboardLayoutDto>
{
    public Task<DashboardLayoutDto> Handle(GetDashboardLayoutQuery request, CancellationToken cancellationToken)
        => dashboardService.GetLayoutAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, cancellationToken);
}

/// <summary>GET /api/v1/dashboard/widgets/hearings-today?date=.</summary>
public sealed record GetHearingsTodayWidgetQuery(DateOnly Date) : IRequest<IReadOnlyList<HearingTodayItemDto>>;

public sealed class GetHearingsTodayWidgetQueryHandler(IDashboardService dashboardService, ICurrentUserService currentUser) : IRequestHandler<GetHearingsTodayWidgetQuery, IReadOnlyList<HearingTodayItemDto>>
{
    public Task<IReadOnlyList<HearingTodayItemDto>> Handle(GetHearingsTodayWidgetQuery request, CancellationToken cancellationToken)
        => dashboardService.GetHearingsTodayAsync(currentUser.TenantId!.Value, request.Date, cancellationToken);
}

/// <summary>GET /api/v1/dashboard/widgets/tasks-pending.</summary>
public sealed record GetTasksPendingWidgetQuery : IRequest<IReadOnlyList<TaskPendingItemDto>>;

public sealed class GetTasksPendingWidgetQueryHandler(IDashboardService dashboardService, ICurrentUserService currentUser) : IRequestHandler<GetTasksPendingWidgetQuery, IReadOnlyList<TaskPendingItemDto>>
{
    public Task<IReadOnlyList<TaskPendingItemDto>> Handle(GetTasksPendingWidgetQuery request, CancellationToken cancellationToken)
        => dashboardService.GetTasksPendingAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, cancellationToken);
}

/// <summary>GET /api/v1/dashboard/widgets/deadlines?days=.</summary>
public sealed record GetDeadlinesWidgetQuery(int Days) : IRequest<IReadOnlyList<DeadlineItemDto>>;

public sealed class GetDeadlinesWidgetQueryHandler(IDashboardService dashboardService, ICurrentUserService currentUser) : IRequestHandler<GetDeadlinesWidgetQuery, IReadOnlyList<DeadlineItemDto>>
{
    public Task<IReadOnlyList<DeadlineItemDto>> Handle(GetDeadlinesWidgetQuery request, CancellationToken cancellationToken)
        => dashboardService.GetDeadlinesAsync(currentUser.TenantId!.Value, request.Days, cancellationToken);
}

/// <summary>GET /api/v1/dashboard/widgets/activity?limit=.</summary>
public sealed record GetActivityWidgetQuery(int Limit) : IRequest<IReadOnlyList<ActivityItemDto>>;

public sealed class GetActivityWidgetQueryHandler(IDashboardService dashboardService, ICurrentUserService currentUser) : IRequestHandler<GetActivityWidgetQuery, IReadOnlyList<ActivityItemDto>>
{
    public Task<IReadOnlyList<ActivityItemDto>> Handle(GetActivityWidgetQuery request, CancellationToken cancellationToken)
        => dashboardService.GetActivityAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Limit, cancellationToken);
}

/// <summary>GET /api/v1/dashboard/widgets/case-stats?range=&amp;start=&amp;end=.</summary>
public sealed record GetCaseStatsWidgetQuery(DateOnly Start, DateOnly End) : IRequest<CaseStatsSummaryDto>;

public sealed class GetCaseStatsWidgetQueryHandler(IDashboardService dashboardService, ICurrentUserService currentUser) : IRequestHandler<GetCaseStatsWidgetQuery, CaseStatsSummaryDto>
{
    public Task<CaseStatsSummaryDto> Handle(GetCaseStatsWidgetQuery request, CancellationToken cancellationToken)
        => dashboardService.GetCaseStatsAsync(currentUser.TenantId!.Value, request.Start, request.End, cancellationToken);
}

/// <summary>GET /api/v1/dashboard/widgets/lawyer-performance?range=&amp;start=&amp;end=.</summary>
public sealed record GetLawyerPerformanceWidgetQuery(DateOnly Start, DateOnly End) : IRequest<IReadOnlyList<LawyerPerformanceItemDto>>;

public sealed class GetLawyerPerformanceWidgetQueryHandler(IDashboardService dashboardService, ICurrentUserService currentUser) : IRequestHandler<GetLawyerPerformanceWidgetQuery, IReadOnlyList<LawyerPerformanceItemDto>>
{
    public Task<IReadOnlyList<LawyerPerformanceItemDto>> Handle(GetLawyerPerformanceWidgetQuery request, CancellationToken cancellationToken)
        => dashboardService.GetLawyerPerformanceAsync(currentUser.TenantId!.Value, request.Start, request.End, cancellationToken);
}

/// <summary>GET /api/v1/dashboard/widgets/matter-summary?range=&amp;start=&amp;end=.</summary>
public sealed record GetMatterSummaryWidgetQuery(DateOnly Start, DateOnly End) : IRequest<IReadOnlyList<MatterSummaryItemDto>>;

public sealed class GetMatterSummaryWidgetQueryHandler(IDashboardService dashboardService, ICurrentUserService currentUser) : IRequestHandler<GetMatterSummaryWidgetQuery, IReadOnlyList<MatterSummaryItemDto>>
{
    public Task<IReadOnlyList<MatterSummaryItemDto>> Handle(GetMatterSummaryWidgetQuery request, CancellationToken cancellationToken)
        => dashboardService.GetMatterSummaryAsync(currentUser.TenantId!.Value, request.Start, request.End, cancellationToken);
}

/// <summary>GET /api/v1/dashboard/widgets/client-summary?range=&amp;start=&amp;end=.</summary>
public sealed record GetClientSummaryWidgetQuery(DateOnly Start, DateOnly End) : IRequest<ClientSummaryWidgetDataDto>;

public sealed class GetClientSummaryWidgetQueryHandler(IDashboardService dashboardService, ICurrentUserService currentUser) : IRequestHandler<GetClientSummaryWidgetQuery, ClientSummaryWidgetDataDto>
{
    public Task<ClientSummaryWidgetDataDto> Handle(GetClientSummaryWidgetQuery request, CancellationToken cancellationToken)
        => dashboardService.GetClientSummaryAsync(currentUser.TenantId!.Value, request.Start, request.End, cancellationToken);
}

/// <summary>GET /api/v1/dashboard/widgets/lead-pipeline?range=&amp;start=&amp;end=.</summary>
public sealed record GetLeadPipelineWidgetQuery(DateOnly Start, DateOnly End) : IRequest<IReadOnlyList<LeadPipelineStageDto>>;

public sealed class GetLeadPipelineWidgetQueryHandler(IDashboardService dashboardService, ICurrentUserService currentUser) : IRequestHandler<GetLeadPipelineWidgetQuery, IReadOnlyList<LeadPipelineStageDto>>
{
    public Task<IReadOnlyList<LeadPipelineStageDto>> Handle(GetLeadPipelineWidgetQuery request, CancellationToken cancellationToken)
        => dashboardService.GetLeadPipelineAsync(currentUser.TenantId!.Value, request.Start, request.End, cancellationToken);
}
