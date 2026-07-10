using System.Text;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Crm;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Crm;

/// <summary>AC-L5 (CSV import: valid rows commit, invalid rows produce a downloadable error CSV), against EF InMemory + an in-memory fake blob store.</summary>
public sealed class LeadImportServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task RunImportAsync_commits_valid_rows_and_writes_an_error_csv_for_invalid_ones()
    {
        await using var db = CreateContext(nameof(RunImportAsync_commits_valid_rows_and_writes_an_error_csv_for_invalid_ones));
        var blobStore = new FakeBlobStorageService();
        var service = new LeadImportService(db, blobStore, new FakeBackgroundJobClient());
        var tenantId = Guid.NewGuid();

        const string csv = "FirstName,LastName,Company,Email,Phone,IssueSummary\n" +
                            "Asha,Rao,,asha@example.com,+919876543210,Contract review\n" +
                            ",NoFirstName,,noone@example.com,,Missing first name\n";

        var batch = await service.EnqueueImportAsync(tenantId, null, "leads.csv", Encoding.UTF8.GetBytes(csv), CancellationToken.None);
        await service.RunImportAsync(tenantId, batch.Id, CancellationToken.None);

        var completed = await service.GetBatchAsync(tenantId, batch.Id, CancellationToken.None);
        completed.Should().NotBeNull();
        completed!.Status.Should().Be("Completed");
        completed.TotalRows.Should().Be(2);
        completed.SuccessCount.Should().Be(1);
        completed.ErrorCount.Should().Be(1);
        completed.ErrorFileBlobPath.Should().NotBeNullOrEmpty();

        var leads = await db.Leads.Where(l => l.TenantId == tenantId).ToListAsync();
        leads.Should().ContainSingle(l => l.FirstName == "Asha");

        var errorCsv = Encoding.UTF8.GetString(await blobStore.DownloadAsync("lead-imports", completed.ErrorFileBlobPath!, CancellationToken.None));
        errorCsv.Should().Contain("FirstName is required");
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        private readonly Dictionary<string, byte[]> _blobs = new();

        public Task<string> UploadAsync(string container, string blobPath, byte[] content, string contentType, CancellationToken cancellationToken = default)
        {
            _blobs[$"{container}/{blobPath}"] = content;
            return Task.FromResult(blobPath);
        }

        public Task<byte[]> DownloadAsync(string container, string blobPath, CancellationToken cancellationToken = default)
            => Task.FromResult(_blobs[$"{container}/{blobPath}"]);

        public Task<string> GetDownloadUrlAsync(string container, string blobPath, TimeSpan validFor, CancellationToken cancellationToken = default)
            => Task.FromResult($"https://fake-blob.test/{container}/{blobPath}");
    }

    private sealed class FakeBackgroundJobClient : IBackgroundJobClient
    {
        public string Create(Job job, IState state) => Guid.NewGuid().ToString();

        public bool ChangeState(string jobId, IState state, string? expectedState) => true;
    }
}
