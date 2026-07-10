using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Reports;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Reports;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 13: reports catalog, standard/custom report execution, run status, and export.</summary>
[ApiController]
[Route("api/v1/reports")]
public sealed class ReportsController(IMediator mediator) : ControllerBase
{
    [HttpGet("catalog")]
    [RequirePermission("reports.operational.own")]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<ReportCatalogItem>>.Of(await mediator.Send(new GetReportCatalogQuery(), cancellationToken)));

    [HttpPost("{key}/run")]
    [RequirePermission("reports.operational.own")]
    public async Task<IActionResult> RunStandard(string key, [FromBody] ReportRunParams reportParams, CancellationToken cancellationToken)
        => Ok(ApiResponse<ReportRunOutcome>.Of(await mediator.Send(new RunStandardReportCommand(key, reportParams), cancellationToken)));

    [HttpGet("runs/{jobId:guid}")]
    [RequirePermission("reports.operational.own")]
    public async Task<IActionResult> GetRun(Guid jobId, CancellationToken cancellationToken)
    {
        var run = await mediator.Send(new GetReportRunQuery(jobId), cancellationToken);
        return run is null ? NotFound() : Ok(ApiResponse<ReportRunDto>.Of(run));
    }

    [HttpPost("custom")]
    [RequirePermission("reports.operational.own")]
    public async Task<IActionResult> CreateCustom([FromBody] CustomReportDefinitionInput input, CancellationToken cancellationToken)
        => Ok(ApiResponse<ReportDefinitionDto>.Of(await mediator.Send(new CreateCustomReportCommand(input), cancellationToken)));

    [HttpPut("custom/{id:guid}")]
    [RequirePermission("reports.operational.own")]
    public async Task<IActionResult> UpdateCustom(Guid id, [FromBody] CustomReportDefinitionInput input, CancellationToken cancellationToken)
        => Ok(ApiResponse<ReportDefinitionDto>.Of(await mediator.Send(new UpdateCustomReportCommand(id, input), cancellationToken)));

    [HttpGet("custom")]
    [RequirePermission("reports.operational.own")]
    public async Task<IActionResult> GetCustomReports(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<ReportDefinitionDto>>.Of(await mediator.Send(new GetCustomReportsQuery(), cancellationToken)));

    [HttpPost("custom/{id:guid}/run")]
    [RequirePermission("reports.operational.own")]
    public async Task<IActionResult> RunCustom(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<ReportRunOutcome>.Of(await mediator.Send(new RunCustomReportCommand(id), cancellationToken)));

    /// <summary>PRD names this "POST /api/v1/reports/{id}/schedule"; the request body carries either ReportKey or ReportDefinitionId, so a single body-driven route covers both standard and custom schedules without an ambiguous {id} that could mean either a report key string or a definition guid.</summary>
    [HttpPost("schedule")]
    [RequirePermission("reports.operational.own")]
    public async Task<IActionResult> CreateSchedule([FromBody] ReportScheduleInput input, CancellationToken cancellationToken)
        => Ok(ApiResponse<ReportScheduleDto>.Of(await mediator.Send(new CreateReportScheduleCommand(input), cancellationToken)));

    [HttpGet("schedules")]
    [RequirePermission("reports.operational.own")]
    public async Task<IActionResult> GetSchedules(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<ReportScheduleDto>>.Of(await mediator.Send(new GetReportSchedulesQuery(), cancellationToken)));

    [HttpGet("export/{runId:guid}")]
    [RequirePermission("reports.operational.own")]
    public async Task<IActionResult> Export(Guid runId, [FromQuery] string format, [FromServices] IReportRunService reportRunService, [FromServices] ICurrentUserService currentUser, CancellationToken cancellationToken)
    {
        var file = await reportRunService.ExportAsync(currentUser.TenantId!.Value, runId, format, cancellationToken);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }
}
