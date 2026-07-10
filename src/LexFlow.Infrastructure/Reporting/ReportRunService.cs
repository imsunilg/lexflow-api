using System.Text.Json;
using Hangfire;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Reporting;

/// <summary>
/// Module 13 run orchestration. <see cref="RunStandardAsync"/>/<see cref="RunCustomAsync"/> race
/// the actual report computation against <see cref="SyncTimeout"/> (default 120 s, per Error
/// Handling: "Run timeout 120 s -&gt; auto-converts to async job with notify"); whichever finishes
/// first wins. A result exceeding <see cref="RowCap"/> (100k, Validation) is also converted to an
/// async job even though it computed within the timeout, so the caller always gets a jobId rather
/// than a multi-hundred-thousand-row inline payload. <see cref="ExecuteQueuedRunAsync"/> is the
/// Hangfire job body — enqueued against this same interface, exactly like ILeadImportService.
/// </summary>
public sealed class ReportRunService(
    LexFlowDbContext db,
    IStandardReportService standardReportService,
    ICustomReportService customReportService,
    IReportScopeService scopeService,
    IReportExportService exportService,
    IBackgroundJobClient backgroundJobClient,
    INotificationService notificationService,
    IBlobStorageService blobStorage,
    TimeSpan? syncTimeout = null) : IReportRunService
{
    private const string Container = "report-exports";
    private const int RowCap = 100_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TimeSpan _syncTimeout = syncTimeout ?? TimeSpan.FromSeconds(120);

    public async Task<ReportRunOutcome> RunStandardAsync(Guid tenantId, Guid userId, string reportKey, ReportRunParams reportParams, CancellationToken cancellationToken = default)
    {
        var catalogItem = standardReportService.GetCatalog().SingleOrDefault(c => string.Equals(c.Key, reportKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("Report", reportKey);

        var scope = await scopeService.ResolveAsync(tenantId, userId, catalogItem.RequiresFinancialPermission ? "financial" : "operational", cancellationToken);
        if (scope.IsDenied)
        {
            throw new ForbiddenAccessException();
        }

        var run = new ReportRun(tenantId, catalogItem.Key, null, JsonSerializer.Serialize(reportParams, JsonOptions), userId, null);
        await db.ReportRuns.AddAsync(run, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return await ExecuteOrConvertAsync(run, () => standardReportService.RunAsync(tenantId, catalogItem.Key, reportParams, scope, cancellationToken), cancellationToken);
    }

    public async Task<ReportRunOutcome> RunCustomAsync(Guid tenantId, Guid userId, Guid definitionId, CancellationToken cancellationToken = default)
    {
        var definition = await customReportService.GetAsync(tenantId, definitionId, cancellationToken)
            ?? throw new NotFoundException(nameof(ReportDefinition), definitionId);

        var scope = await scopeService.ResolveAsync(tenantId, userId, PermissionAreaFor(definition.BaseEntity), cancellationToken);
        if (scope.IsDenied)
        {
            throw new ForbiddenAccessException();
        }

        var run = new ReportRun(tenantId, null, definitionId, "{}", userId, null);
        await db.ReportRuns.AddAsync(run, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return await ExecuteOrConvertAsync(run, () => customReportService.RunAsync(tenantId, definitionId, scope, cancellationToken), cancellationToken);
    }

    public async Task<ReportRunDto?> GetRunAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await db.ReportRuns.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == runId, cancellationToken);
        return run is null ? null : ToDto(run);
    }

    public async Task ExecuteQueuedRunAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await db.ReportRuns.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == runId, cancellationToken)
            ?? throw new NotFoundException(nameof(ReportRun), runId);

        try
        {
            if (run.Status == "Queued")
            {
                run.Start();
                await db.SaveChangesAsync(cancellationToken);
            }

            var result = await ComputeAsync(run, cancellationToken);

            string? blobPath = null;
            if (run.Format is not null)
            {
                var content = exportService.Render(result, await BuildContextAsync(tenantId, run, cancellationToken), run.Format);
                blobPath = await blobStorage.UploadAsync(Container, $"{tenantId:N}/{run.Id:N}.{run.Format}", content, ContentTypeFor(run.Format), cancellationToken);
            }

            run.Complete(result.RowCount, blobPath);
            await db.SaveChangesAsync(cancellationToken);

            if (run.RequestedBy.HasValue)
            {
                await notificationService.NotifyAsync(
                    tenantId,
                    run.RequestedBy.Value,
                    new NotifyRequest("report.run.completed", "Your report is ready", $"{run.ReportKey ?? "Custom report"} finished with {result.RowCount} rows.", $"/reports/runs/{run.Id}", ["Email", "InApp"]),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            run.Fail(ex.Message);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<ExportedFile?> ExportAsync(Guid tenantId, Guid runId, string format, CancellationToken cancellationToken = default)
    {
        var run = await db.ReportRuns.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == runId, cancellationToken);
        if (run is null || run.Status != "Completed")
        {
            return null;
        }

        if (run.ResultBlobPath is not null && string.Equals(run.Format, format, StringComparison.OrdinalIgnoreCase))
        {
            var content = await blobStorage.DownloadAsync(Container, run.ResultBlobPath, cancellationToken);
            return new ExportedFile(content, ContentTypeFor(format), FileNameFor(run, format));
        }

        // Inline runs (and runs exported in a different format than they were first rendered in)
        // don't have a persisted export yet — re-run the same query on demand and render it now.
        var result = await ComputeAsync(run, cancellationToken);
        var rendered = exportService.Render(result, await BuildContextAsync(tenantId, run, cancellationToken), format);
        return new ExportedFile(rendered, ContentTypeFor(format), FileNameFor(run, format));
    }

    private async Task<ReportRunOutcome> ExecuteOrConvertAsync(ReportRun run, Func<Task<ReportResult>> compute, CancellationToken cancellationToken)
    {
        run.Start();
        await db.SaveChangesAsync(cancellationToken);

        var computeTask = compute();
        var delayTask = Task.Delay(_syncTimeout, cancellationToken);
        var winner = await Task.WhenAny(computeTask, delayTask);

        if (winner == computeTask)
        {
            var result = await computeTask;
            if (result.RowCount <= RowCap)
            {
                run.Complete(result.RowCount, null);
                await db.SaveChangesAsync(cancellationToken);
                return new ReportRunOutcome(run.Id, "Completed", result);
            }
        }

        // Either the timeout won the race, or the result exceeded the row cap — both convert to
        // an async job; the run row stays Running until the Hangfire job below flips it.
        backgroundJobClient.Enqueue<IReportRunService>(s => s.ExecuteQueuedRunAsync(run.TenantId, run.Id, CancellationToken.None));
        return new ReportRunOutcome(run.Id, "Queued", null);
    }

    private async Task<ReportResult> ComputeAsync(ReportRun run, CancellationToken cancellationToken)
    {
        if (run.ReportKey is not null)
        {
            var catalogItem = standardReportService.GetCatalog().Single(c => c.Key == run.ReportKey);
            var scope = await scopeService.ResolveAsync(run.TenantId, run.RequestedBy ?? Guid.Empty, catalogItem.RequiresFinancialPermission ? "financial" : "operational", cancellationToken);
            var reportParams = JsonSerializer.Deserialize<ReportRunParams>(run.ParamsJson, JsonOptions) ?? new ReportRunParams(null, null, null, null, null, null);
            return await standardReportService.RunAsync(run.TenantId, run.ReportKey, reportParams, scope, cancellationToken);
        }

        var definition = await customReportService.GetAsync(run.TenantId, run.ReportDefinitionId!.Value, cancellationToken)
            ?? throw new NotFoundException(nameof(ReportDefinition), run.ReportDefinitionId!.Value);
        var customScope = await scopeService.ResolveAsync(run.TenantId, run.RequestedBy ?? Guid.Empty, PermissionAreaFor(definition.BaseEntity), cancellationToken);
        return await customReportService.RunAsync(run.TenantId, run.ReportDefinitionId!.Value, customScope, cancellationToken);
    }

    private async Task<ReportExportContext> BuildContextAsync(Guid tenantId, ReportRun run, CancellationToken cancellationToken)
    {
        var tenantName = await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.Name).SingleOrDefaultAsync(cancellationToken) ?? "LexFlow";
        var title = run.ReportKey ?? "Custom Report";
        return new ReportExportContext(title, tenantName, run.ParamsJson, DateTimeOffset.UtcNow, "UTC");
    }

    private static string PermissionAreaFor(string baseEntity) => string.Equals(baseEntity, "Invoice", StringComparison.OrdinalIgnoreCase) ? "financial" : "operational";

    private static string ContentTypeFor(string format) => format.ToLowerInvariant() switch
    {
        "pdf" => "application/pdf",
        "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "csv" => "text/csv",
        _ => "application/octet-stream",
    };

    private static string FileNameFor(ReportRun run, string format) => $"{(run.ReportKey ?? "custom-report")}-{run.Id:N}.{format.ToLowerInvariant()}";

    private static ReportRunDto ToDto(ReportRun run) =>
        new(run.Id, run.ReportKey, run.ReportDefinitionId, run.Status, run.RowCount, run.ErrorMessage, run.RequestedAt, run.CompletedAt);
}
