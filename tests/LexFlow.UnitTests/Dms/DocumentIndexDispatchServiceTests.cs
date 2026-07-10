using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Dms;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Dms;

/// <summary>Consumer half of the outbox pattern — a Pending row gets dispatched exactly once and marked Done/Failed.</summary>
public sealed class DocumentIndexDispatchServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task DispatchPendingAsync_indexes_a_pending_row_and_marks_it_done()
    {
        await using var db = CreateContext(nameof(DispatchPendingAsync_indexes_a_pending_row_and_marks_it_done));
        var tenantId = Guid.NewGuid();

        var document = new Document(tenantId, null, null, null, null, "Contract v1", "Agreement", "Normal");
        await db.Documents.AddAsync(document);
        await db.SaveChangesAsync();

        var outbox = new DocumentIndexOutbox(tenantId, document.Id, null, "Index");
        await db.DocumentIndexOutbox.AddAsync(outbox);
        await db.SaveChangesAsync();

        var indexer = new FakeDocumentIndexer();
        var dispatcher = new DocumentIndexDispatchService(db, indexer, new FakeBlobStorageService());

        await dispatcher.DispatchPendingAsync(CancellationToken.None);

        indexer.IndexedDocumentIds.Should().ContainSingle().Which.Should().Be(document.Id);

        var reloaded = await db.DocumentIndexOutbox.SingleAsync(o => o.Id == outbox.Id);
        reloaded.Status.Should().Be("Done");
        reloaded.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DispatchPendingAsync_calls_delete_for_a_delete_operation()
    {
        await using var db = CreateContext(nameof(DispatchPendingAsync_calls_delete_for_a_delete_operation));
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var outbox = new DocumentIndexOutbox(tenantId, documentId, null, "Delete");
        await db.DocumentIndexOutbox.AddAsync(outbox);
        await db.SaveChangesAsync();

        var indexer = new FakeDocumentIndexer();
        var dispatcher = new DocumentIndexDispatchService(db, indexer, new FakeBlobStorageService());

        await dispatcher.DispatchPendingAsync(CancellationToken.None);

        indexer.DeletedDocumentIds.Should().ContainSingle().Which.Should().Be(documentId);
        var reloaded = await db.DocumentIndexOutbox.SingleAsync(o => o.Id == outbox.Id);
        reloaded.Status.Should().Be("Done");
    }

    [Fact]
    public async Task DispatchPendingAsync_marks_a_row_failed_when_the_indexer_throws()
    {
        await using var db = CreateContext(nameof(DispatchPendingAsync_marks_a_row_failed_when_the_indexer_throws));
        var tenantId = Guid.NewGuid();

        var document = new Document(tenantId, null, null, null, null, "Contract v1", "Agreement", "Normal");
        await db.Documents.AddAsync(document);
        await db.SaveChangesAsync();

        var outbox = new DocumentIndexOutbox(tenantId, document.Id, null, "Index");
        await db.DocumentIndexOutbox.AddAsync(outbox);
        await db.SaveChangesAsync();

        var indexer = new FakeDocumentIndexer(throwOnIndex: true);
        var dispatcher = new DocumentIndexDispatchService(db, indexer, new FakeBlobStorageService());

        await dispatcher.DispatchPendingAsync(CancellationToken.None);

        var reloaded = await db.DocumentIndexOutbox.SingleAsync(o => o.Id == outbox.Id);
        reloaded.Status.Should().Be("Failed");
        reloaded.LastError.Should().NotBeNullOrEmpty();
    }

    private sealed class FakeDocumentIndexer(bool throwOnIndex = false) : IDocumentIndexer
    {
        public List<Guid> IndexedDocumentIds { get; } = [];
        public List<Guid> DeletedDocumentIds { get; } = [];

        public Task IndexAsync(Guid tenantId, Guid documentId, DocumentIndexPayload payload, CancellationToken cancellationToken = default)
        {
            if (throwOnIndex)
            {
                throw new InvalidOperationException("Simulated ES failure.");
            }

            IndexedDocumentIds.Add(documentId);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken = default)
        {
            DeletedDocumentIds.Add(documentId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DocumentSearchHit>> SearchAsync(Guid tenantId, DocumentSearchQuery query, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DocumentSearchHit>>([]);
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        public Task<string> UploadAsync(string container, string blobPath, byte[] content, string contentType, CancellationToken cancellationToken = default)
            => Task.FromResult(blobPath);

        public Task<byte[]> DownloadAsync(string container, string blobPath, CancellationToken cancellationToken = default)
            => throw new FileNotFoundException("No companion text blob in this fake.");

        public Task<string> GetDownloadUrlAsync(string container, string blobPath, TimeSpan validFor, CancellationToken cancellationToken = default)
            => Task.FromResult($"https://fake-blob.test/{container}/{blobPath}");
    }
}
