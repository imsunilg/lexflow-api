using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using LexFlow.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Reporting;

/// <summary>
/// Module 13 Error Handling: "Run timeout 120 s -&gt; auto-converts to async job with notify."
/// Validation: "row cap 100k per export (larger -&gt; async + email link)." Uses a short injected
/// sync timeout instead of the real 120 s so the conversion path is exercised in milliseconds.
/// </summary>
public sealed class ReportRunServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static ReportScope AllScope => new("all", null, null);

    private static ReportRunService CreateService(
        LexFlowDbContext db,
        IStandardReportService? standard = null,
        FakeBackgroundJobClient? jobClient = null,
        FakeNotificationService? notifications = null,
        TimeSpan? syncTimeout = null) =>
        new(
            db,
            standard ?? new FakeStandardReportService(new ReportResult(["Col"], [["v"]]), TimeSpan.Zero),
            new FakeCustomReportService(),
            new FakeReportScopeService(AllScope),
            new FakeReportExportService(),
            jobClient ?? new FakeBackgroundJobClient(),
            notifications ?? new FakeNotificationService(),
            new FakeBlobStorageService(),
            syncTimeout);

    [Fact]
    public async Task RunStandardAsync_completes_inline_when_the_report_finishes_within_the_timeout()
    {
        await using var db = CreateContext(nameof(RunStandardAsync_completes_inline_when_the_report_finishes_within_the_timeout));
        var fastReport = new FakeStandardReportService(new ReportResult(["Col"], [["v1"], ["v2"]]), TimeSpan.Zero);
        var service = CreateService(db, fastReport, syncTimeout: TimeSpan.FromSeconds(5));
        var tenantId = Guid.NewGuid();

        var outcome = await service.RunStandardAsync(tenantId, Guid.NewGuid(), "revenue", new ReportRunParams(null, null, null, null, null, null), CancellationToken.None);

        outcome.Status.Should().Be("Completed");
        outcome.InlineResult.Should().NotBeNull();
        outcome.InlineResult!.RowCount.Should().Be(2);

        var run = await db.ReportRuns.SingleAsync(r => r.Id == outcome.RunId);
        run.Status.Should().Be("Completed");
        run.RowCount.Should().Be(2);
    }

    [Fact]
    public async Task RunStandardAsync_converts_to_an_async_job_when_the_report_exceeds_the_sync_timeout()
    {
        await using var db = CreateContext(nameof(RunStandardAsync_converts_to_an_async_job_when_the_report_exceeds_the_sync_timeout));
        var slowReport = new FakeStandardReportService(new ReportResult(["Col"], [["v"]]), TimeSpan.FromSeconds(2));
        var jobClient = new FakeBackgroundJobClient();
        var service = CreateService(db, slowReport, jobClient, syncTimeout: TimeSpan.FromMilliseconds(30));
        var tenantId = Guid.NewGuid();

        var outcome = await service.RunStandardAsync(tenantId, Guid.NewGuid(), "revenue", new ReportRunParams(null, null, null, null, null, null), CancellationToken.None);

        outcome.Status.Should().Be("Queued");
        outcome.InlineResult.Should().BeNull();
        jobClient.EnqueuedJobs.Should().ContainSingle();
    }

    [Fact]
    public async Task RunStandardAsync_converts_to_an_async_job_when_the_row_cap_is_exceeded_even_if_it_finished_fast()
    {
        await using var db = CreateContext(nameof(RunStandardAsync_converts_to_an_async_job_when_the_row_cap_is_exceeded_even_if_it_finished_fast));
        var hugeRows = Enumerable.Range(0, 100_001).Select(i => (IReadOnlyList<object?>)new object?[] { i }).ToList();
        var hugeReport = new FakeStandardReportService(new ReportResult(["Col"], hugeRows), TimeSpan.Zero);
        var jobClient = new FakeBackgroundJobClient();
        var service = CreateService(db, hugeReport, jobClient, syncTimeout: TimeSpan.FromSeconds(5));

        var outcome = await service.RunStandardAsync(Guid.NewGuid(), Guid.NewGuid(), "revenue", new ReportRunParams(null, null, null, null, null, null), CancellationToken.None);

        outcome.Status.Should().Be("Queued");
        jobClient.EnqueuedJobs.Should().ContainSingle();
    }

    [Fact]
    public async Task RunStandardAsync_throws_forbidden_when_the_scope_service_denies_access()
    {
        await using var db = CreateContext(nameof(RunStandardAsync_throws_forbidden_when_the_scope_service_denies_access));
        var service = new ReportRunService(
            db,
            new FakeStandardReportService(new ReportResult([], []), TimeSpan.Zero),
            new FakeCustomReportService(),
            new FakeReportScopeService(ReportScope.None()),
            new FakeReportExportService(),
            new FakeBackgroundJobClient(),
            new FakeNotificationService(),
            new FakeBlobStorageService());

        var act = () => service.RunStandardAsync(Guid.NewGuid(), Guid.NewGuid(), "revenue", new ReportRunParams(null, null, null, null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task ExecuteQueuedRunAsync_completes_the_run_and_notifies_the_requester()
    {
        await using var db = CreateContext(nameof(ExecuteQueuedRunAsync_completes_the_run_and_notifies_the_requester));
        var notifications = new FakeNotificationService();
        var service = CreateService(db, notifications: notifications, syncTimeout: TimeSpan.FromMilliseconds(10));
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var run = new ReportRun(tenantId, "revenue", null, "{}", userId, null);
        await db.ReportRuns.AddAsync(run);
        await db.SaveChangesAsync();

        await service.ExecuteQueuedRunAsync(tenantId, run.Id, CancellationToken.None);

        var reloaded = await db.ReportRuns.SingleAsync(r => r.Id == run.Id);
        reloaded.Status.Should().Be("Completed");
        notifications.Notified.Should().ContainSingle(n => n.UserId == userId);
    }

    [Fact]
    public async Task GetRunAsync_returns_null_for_an_unknown_run()
    {
        await using var db = CreateContext(nameof(GetRunAsync_returns_null_for_an_unknown_run));
        var service = CreateService(db);

        var run = await service.GetRunAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        run.Should().BeNull();
    }

    private sealed class FakeStandardReportService(ReportResult result, TimeSpan delay) : IStandardReportService
    {
        public IReadOnlyList<ReportCatalogItem> GetCatalog() => [new("revenue", "Revenue", "Finance", true)];

        public async Task<ReportResult> RunAsync(Guid tenantId, string reportKey, ReportRunParams reportParams, ReportScope scope, CancellationToken cancellationToken = default)
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, CancellationToken.None);
            }

            return result;
        }
    }

    private sealed class FakeCustomReportService : ICustomReportService
    {
        public Task<ReportDefinitionDto> CreateAsync(Guid tenantId, Guid ownerId, CustomReportDefinitionInput input, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<ReportDefinitionDto> UpdateAsync(Guid tenantId, Guid definitionId, CustomReportDefinitionInput input, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<ReportDefinitionDto>> GetForOwnerAsync(Guid tenantId, Guid ownerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<ReportDefinitionDto?> GetAsync(Guid tenantId, Guid definitionId, CancellationToken cancellationToken = default) => Task.FromResult<ReportDefinitionDto?>(null);

        public Task<ReportResult> RunAsync(Guid tenantId, Guid definitionId, ReportScope scope, CancellationToken cancellationToken = default) => Task.FromResult(new ReportResult([], []));
    }

    private sealed class FakeReportScopeService(ReportScope scope) : IReportScopeService
    {
        public Task<ReportScope> ResolveAsync(Guid tenantId, Guid userId, string permissionArea, CancellationToken cancellationToken = default) => Task.FromResult(scope);
    }

    private sealed class FakeReportExportService : IReportExportService
    {
        public byte[] Render(ReportResult result, ReportExportContext context, string format) => [1, 2, 3];
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        public Task<string> UploadAsync(string container, string blobPath, byte[] content, string contentType, CancellationToken cancellationToken = default) => Task.FromResult(blobPath);

        public Task<byte[]> DownloadAsync(string container, string blobPath, CancellationToken cancellationToken = default) => Task.FromResult<byte[]>([1, 2, 3]);

        public Task<string> GetDownloadUrlAsync(string container, string blobPath, TimeSpan validFor, CancellationToken cancellationToken = default) => Task.FromResult($"https://fake/{container}/{blobPath}");
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public List<(Guid UserId, NotifyRequest Request)> Notified { get; } = [];

        public Task<NotificationDispatchResult> NotifyAsync(Guid tenantId, Guid userId, NotifyRequest request, CancellationToken cancellationToken = default)
        {
            Notified.Add((userId, request));
            return Task.FromResult(new NotificationDispatchResult(Guid.NewGuid(), request.Channels, []));
        }

        public Task<IReadOnlyList<NotificationDto>> GetForUserAsync(Guid tenantId, Guid userId, bool unreadOnly, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NotificationDto>>([]);

        public Task MarkReadAsync(Guid tenantId, Guid userId, Guid notificationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeBackgroundJobClient : IBackgroundJobClient
    {
        public List<Job> EnqueuedJobs { get; } = [];

        public string Create(Job job, IState state)
        {
            EnqueuedJobs.Add(job);
            return Guid.NewGuid().ToString();
        }

        public bool ChangeState(string jobId, IState state, string? expectedState) => true;
    }
}
