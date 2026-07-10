using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Kb;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Kb;

/// <summary>Module 12: draft -&gt; peer review -&gt; publish. Peer-review rule (reviewer≠author) and AC-KB5 (unreviewed article never visible to non-authors).</summary>
public sealed class KbArticleServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static KbArticleService CreateService(LexFlowDbContext db, FakeKbSearchIndexer? indexer = null) => new(db, indexer ?? new FakeKbSearchIndexer());

    [Fact]
    public async Task AssignReviewerAsync_throws_when_reviewer_equals_author()
    {
        await using var db = CreateContext(nameof(AssignReviewerAsync_throws_when_reviewer_equals_author));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var article = await service.CreateDraftAsync(tenantId, authorId, "Cheque Bounce Defense", "Body", CancellationToken.None);
        await service.SubmitForReviewAsync(tenantId, article.Id, CancellationToken.None);

        var act = () => service.AssignReviewerAsync(tenantId, article.Id, authorId, CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "SELF_REVIEW_NOT_ALLOWED");
    }

    [Fact]
    public async Task PublishAsync_throws_when_no_tag_is_attached()
    {
        await using var db = CreateContext(nameof(PublishAsync_throws_when_no_tag_is_attached));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var article = await service.CreateDraftAsync(tenantId, authorId, "Title", "Body", CancellationToken.None);
        await service.SubmitForReviewAsync(tenantId, article.Id, CancellationToken.None);
        await service.AssignReviewerAsync(tenantId, article.Id, reviewerId, CancellationToken.None);

        var act = () => service.PublishAsync(tenantId, article.Id, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PublishAsync_succeeds_with_a_tag_and_a_distinct_reviewer_and_snapshots_a_version()
    {
        await using var db = CreateContext(nameof(PublishAsync_succeeds_with_a_tag_and_a_distinct_reviewer_and_snapshots_a_version));
        var indexer = new FakeKbSearchIndexer();
        var service = CreateService(db, indexer);
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var article = await service.CreateDraftAsync(tenantId, authorId, "Title", "Body", CancellationToken.None);
        await db.KbItemTags.AddAsync(new KbItemTag(tenantId, Guid.NewGuid(), "Article", article.Id));
        await db.SaveChangesAsync();
        await service.SubmitForReviewAsync(tenantId, article.Id, CancellationToken.None);
        await service.AssignReviewerAsync(tenantId, article.Id, reviewerId, CancellationToken.None);

        var published = await service.PublishAsync(tenantId, article.Id, CancellationToken.None);

        published.Status.Should().Be("Published");
        var versions = await service.GetVersionsAsync(tenantId, article.Id, CancellationToken.None);
        versions.Should().ContainSingle(v => v.VersionNo == 1);
        indexer.IndexedDocs.Should().ContainSingle(d => d.Kind == "Article" && d.Id == article.Id);
    }

    [Fact]
    public async Task GetAsync_hides_an_unreviewed_article_from_non_authors_AC_KB5()
    {
        await using var db = CreateContext(nameof(GetAsync_hides_an_unreviewed_article_from_non_authors_AC_KB5));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var article = await service.CreateDraftAsync(tenantId, authorId, "Draft article", "Body", CancellationToken.None);

        var visibleToStranger = await service.GetAsync(tenantId, article.Id, strangerId, callerCanReview: false, CancellationToken.None);
        var visibleToAuthor = await service.GetAsync(tenantId, article.Id, authorId, callerCanReview: false, CancellationToken.None);
        var visibleToReviewer = await service.GetAsync(tenantId, article.Id, strangerId, callerCanReview: true, CancellationToken.None);

        visibleToStranger.Should().BeNull();
        visibleToAuthor.Should().NotBeNull();
        visibleToReviewer.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAsync_is_visible_to_everyone_once_published()
    {
        await using var db = CreateContext(nameof(GetAsync_is_visible_to_everyone_once_published));
        var service = CreateService(db);
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var article = await service.CreateDraftAsync(tenantId, authorId, "Title", "Body", CancellationToken.None);
        await db.KbItemTags.AddAsync(new KbItemTag(tenantId, Guid.NewGuid(), "Article", article.Id));
        await db.SaveChangesAsync();
        await service.SubmitForReviewAsync(tenantId, article.Id, CancellationToken.None);
        await service.AssignReviewerAsync(tenantId, article.Id, reviewerId, CancellationToken.None);
        await service.PublishAsync(tenantId, article.Id, CancellationToken.None);

        var visibleToStranger = await service.GetAsync(tenantId, article.Id, strangerId, callerCanReview: false, CancellationToken.None);

        visibleToStranger.Should().NotBeNull();
    }

    private sealed class FakeKbSearchIndexer : IKbSearchIndexer
    {
        public List<KbSearchDoc> IndexedDocs { get; } = [];

        public Task IndexAsync(Guid tenantId, KbSearchDoc doc, CancellationToken cancellationToken = default)
        {
            IndexedDocs.Add(doc);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid tenantId, string kind, Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<KbSearchHit>> SearchAsync(Guid tenantId, KbSearchQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<KbSearchHit>>([]);
    }
}
