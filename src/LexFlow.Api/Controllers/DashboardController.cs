using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Dashboard;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>
/// Module 1 dashboard — PRD §17. Visible to any authenticated staff user
/// regardless of module-specific permissions (same convention as
/// AuthController's /auth/me: [Authorize] only, no [RequirePermission]),
/// since every widget here only ever surfaces summary/count-level data the
/// user could already see piecemeal via their own module permissions.
/// </summary>
[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public sealed class DashboardController(IMediator mediator) : ControllerBase
{
    [HttpGet("layout")]
    public async Task<IActionResult> GetLayout(CancellationToken cancellationToken)
        => Ok(ApiResponse<DashboardLayoutDto>.Of(await mediator.Send(new GetDashboardLayoutQuery(), cancellationToken)));

    [HttpPut("layout")]
    public async Task<IActionResult> SaveLayout([FromBody] DashboardLayoutDto layout, CancellationToken cancellationToken)
        => Ok(ApiResponse<DashboardLayoutDto>.Of(await mediator.Send(new SaveDashboardLayoutCommand(layout), cancellationToken)));

    [HttpGet("widgets/hearings-today")]
    public async Task<IActionResult> GetHearingsToday([FromQuery] DateOnly? date, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<HearingTodayItemDto>>.Of(await mediator.Send(new GetHearingsTodayWidgetQuery(date ?? DateOnly.FromDateTime(DateTime.UtcNow)), cancellationToken)));

    [HttpGet("widgets/tasks-pending")]
    public async Task<IActionResult> GetTasksPending(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<TaskPendingItemDto>>.Of(await mediator.Send(new GetTasksPendingWidgetQuery(), cancellationToken)));

    [HttpGet("widgets/deadlines")]
    public async Task<IActionResult> GetDeadlines([FromQuery] int days = 14, CancellationToken cancellationToken = default)
        => Ok(ApiResponse<IReadOnlyList<DeadlineItemDto>>.Of(await mediator.Send(new GetDeadlinesWidgetQuery(days), cancellationToken)));

    [HttpGet("widgets/activity")]
    public async Task<IActionResult> GetActivity([FromQuery] int limit = 20, CancellationToken cancellationToken = default)
        => Ok(ApiResponse<IReadOnlyList<ActivityItemDto>>.Of(await mediator.Send(new GetActivityWidgetQuery(limit), cancellationToken)));

    /// <summary>`range` is accepted for forward-compatibility with the frontend's preset selector but the actual window is always `start`/`end` (frontend always supplies both alongside `range`, per DashboardWidgetsService.rangeParams).</summary>
    [HttpGet("widgets/case-stats")]
    public async Task<IActionResult> GetCaseStats([FromQuery] string? range, [FromQuery] DateOnly? start, [FromQuery] DateOnly? end, CancellationToken cancellationToken)
    {
        var (from, to) = ResolveRange(start, end);
        return Ok(ApiResponse<CaseStatsSummaryDto>.Of(await mediator.Send(new GetCaseStatsWidgetQuery(from, to), cancellationToken)));
    }

    [HttpGet("widgets/lawyer-performance")]
    public async Task<IActionResult> GetLawyerPerformance([FromQuery] string? range, [FromQuery] DateOnly? start, [FromQuery] DateOnly? end, CancellationToken cancellationToken)
    {
        var (from, to) = ResolveRange(start, end);
        return Ok(ApiResponse<IReadOnlyList<LawyerPerformanceItemDto>>.Of(await mediator.Send(new GetLawyerPerformanceWidgetQuery(from, to), cancellationToken)));
    }

    [HttpGet("widgets/matter-summary")]
    public async Task<IActionResult> GetMatterSummary([FromQuery] string? range, [FromQuery] DateOnly? start, [FromQuery] DateOnly? end, CancellationToken cancellationToken)
    {
        var (from, to) = ResolveRange(start, end);
        return Ok(ApiResponse<IReadOnlyList<MatterSummaryItemDto>>.Of(await mediator.Send(new GetMatterSummaryWidgetQuery(from, to), cancellationToken)));
    }

    [HttpGet("widgets/client-summary")]
    public async Task<IActionResult> GetClientSummary([FromQuery] string? range, [FromQuery] DateOnly? start, [FromQuery] DateOnly? end, CancellationToken cancellationToken)
    {
        var (from, to) = ResolveRange(start, end);
        return Ok(ApiResponse<ClientSummaryWidgetDataDto>.Of(await mediator.Send(new GetClientSummaryWidgetQuery(from, to), cancellationToken)));
    }

    [HttpGet("widgets/lead-pipeline")]
    public async Task<IActionResult> GetLeadPipeline([FromQuery] string? range, [FromQuery] DateOnly? start, [FromQuery] DateOnly? end, CancellationToken cancellationToken)
    {
        var (from, to) = ResolveRange(start, end);
        return Ok(ApiResponse<IReadOnlyList<LeadPipelineStageDto>>.Of(await mediator.Send(new GetLeadPipelineWidgetQuery(from, to), cancellationToken)));
    }

    private static (DateOnly Start, DateOnly End) ResolveRange(DateOnly? start, DateOnly? end)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return (start ?? today.AddMonths(-1), end ?? today);
    }
}
