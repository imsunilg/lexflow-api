using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Ai;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Ai;

/// <summary>Module 16 RAG: chunking/indexing, similarity ranking, and RBAC-filtered retrieval (a chunk the guard denies must never come back, even if it ranks highest).</summary>
public sealed class RagRetrievalServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task IndexAsync_chunks_long_text_and_replaces_prior_chunks_on_reindex()
    {
        await using var db = CreateContext(nameof(IndexAsync_chunks_long_text_and_replaces_prior_chunks_on_reindex));
        var service = new RagRetrievalService(db, new HashingEmbeddingProvider(), new AllowAllGuard());
        var tenantId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();

        await service.IndexAsync(tenantId, "Document", sourceId, new string('a', 2500), CancellationToken.None);
        var firstPass = await db.AiEmbeddings.Where(e => e.SourceId == sourceId).ToListAsync();

        await service.IndexAsync(tenantId, "Document", sourceId, "short text", CancellationToken.None);
        var secondPass = await db.AiEmbeddings.Where(e => e.SourceId == sourceId).ToListAsync();

        firstPass.Should().HaveCount(3);
        secondPass.Should().ContainSingle();
    }

    [Fact]
    public async Task RetrieveAsync_ranks_the_more_similar_chunk_first()
    {
        await using var db = CreateContext(nameof(RetrieveAsync_ranks_the_more_similar_chunk_first));
        var service = new RagRetrievalService(db, new HashingEmbeddingProvider(), new AllowAllGuard());
        var tenantId = Guid.NewGuid();

        await service.IndexAsync(tenantId, "Document", Guid.NewGuid(), "limitation period bar act statute", CancellationToken.None);
        await service.IndexAsync(tenantId, "Document", Guid.NewGuid(), "banana smoothie recipe blender", CancellationToken.None);

        var results = await service.RetrieveAsync(tenantId, Guid.NewGuid(), [], "limitation statute bar act", topK: 2, cancellationToken: CancellationToken.None);

        results.Should().HaveCount(2);
        results[0].Score.Should().BeGreaterThan(results[1].Score);
        results[0].ChunkText.Should().Contain("limitation");
    }

    [Fact]
    public async Task RetrieveAsync_drops_chunks_the_guard_denies_even_if_they_rank_highest()
    {
        await using var db = CreateContext(nameof(RetrieveAsync_drops_chunks_the_guard_denies_even_if_they_rank_highest));
        var deniedSourceId = Guid.NewGuid();
        var allowedSourceId = Guid.NewGuid();
        var guard = new SelectiveGuard(deny: deniedSourceId);
        var service = new RagRetrievalService(db, new HashingEmbeddingProvider(), guard);
        var tenantId = Guid.NewGuid();

        await service.IndexAsync(tenantId, "Document", deniedSourceId, "contract liability indemnity clause", CancellationToken.None);
        await service.IndexAsync(tenantId, "Document", allowedSourceId, "contract liability indemnity clause", CancellationToken.None);

        var results = await service.RetrieveAsync(tenantId, Guid.NewGuid(), [], "contract liability indemnity clause", topK: 5, cancellationToken: CancellationToken.None);

        results.Should().ContainSingle();
        results.Single().SourceId.Should().Be(allowedSourceId);
    }

    [Fact]
    public async Task RetrieveAsync_respects_the_sourceKinds_filter()
    {
        await using var db = CreateContext(nameof(RetrieveAsync_respects_the_sourceKinds_filter));
        var service = new RagRetrievalService(db, new HashingEmbeddingProvider(), new AllowAllGuard());
        var tenantId = Guid.NewGuid();

        await service.IndexAsync(tenantId, "Document", Guid.NewGuid(), "case law precedent judgment", CancellationToken.None);
        await service.IndexAsync(tenantId, "KbJudgment", Guid.NewGuid(), "case law precedent judgment", CancellationToken.None);

        var results = await service.RetrieveAsync(tenantId, Guid.NewGuid(), [], "case law precedent judgment", topK: 5, sourceKinds: ["KbJudgment"], cancellationToken: CancellationToken.None);

        results.Should().ContainSingle();
        results.Single().SourceKind.Should().Be("KbJudgment");
    }

    private sealed class AllowAllGuard : IAiRetrievalGuard
    {
        public Task<bool> CanAccessAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, string sourceKind, Guid sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class SelectiveGuard(Guid deny) : IAiRetrievalGuard
    {
        public Task<bool> CanAccessAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, string sourceKind, Guid sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(sourceId != deny);
    }
}
