using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Kb;

/// <summary>
/// Module 12: draft -&gt; peer review -&gt; publish, versioned. BR (peer-review rule): "publish
/// requires ≥1 tag + reviewer ≠ author" — reviewer≠author is enforced first at the DB (kb.
/// kb_articles 004_Triggers.sql) and again defensively in KbArticle.Publish; a DbUpdateException
/// carrying the trigger's text is translated to DomainRuleException("SELF_REVIEW_NOT_ALLOWED",
/// ...). AC-KB5: unreviewed (Draft/InReview) articles are visible only to their author or a
/// caller with kb.review — enforced in every read path here, not left to the controller.
/// </summary>
public sealed class KbArticleService(LexFlowDbContext db, IKbSearchIndexer searchIndexer) : IKbArticleService
{
    public async Task<KbArticleDto> CreateDraftAsync(Guid tenantId, Guid authorId, string title, string? body, CancellationToken cancellationToken = default)
    {
        var article = new KbArticle(tenantId, title, body, authorId);
        await db.KbArticles.AddAsync(article, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(article);
    }

    public async Task<KbArticleDto> UpdateDraftAsync(Guid tenantId, Guid articleId, string title, string? body, CancellationToken cancellationToken = default)
    {
        var article = await GetArticleOrThrowAsync(tenantId, articleId, cancellationToken);
        article.UpdateDraft(title, body);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(article);
    }

    public async Task<KbArticleDto> SubmitForReviewAsync(Guid tenantId, Guid articleId, CancellationToken cancellationToken = default)
    {
        var article = await GetArticleOrThrowAsync(tenantId, articleId, cancellationToken);
        article.SubmitForReview();
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(article);
    }

    public async Task<KbArticleDto> AssignReviewerAsync(Guid tenantId, Guid articleId, Guid reviewerId, CancellationToken cancellationToken = default)
    {
        var article = await GetArticleOrThrowAsync(tenantId, articleId, cancellationToken);

        try
        {
            article.AssignReviewer(reviewerId);
        }
        catch (InvalidOperationException) when (reviewerId == article.AuthorId)
        {
            throw new DomainRuleException("SELF_REVIEW_NOT_ALLOWED", $"Article {articleId}'s reviewer must differ from its author (Module 12 peer-review rule).");
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(article);
    }

    public async Task<KbArticleDto> SendBackToDraftAsync(Guid tenantId, Guid articleId, CancellationToken cancellationToken = default)
    {
        var article = await GetArticleOrThrowAsync(tenantId, articleId, cancellationToken);
        article.SendBackToDraft();
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(article);
    }

    public async Task<KbArticleDto> PublishAsync(Guid tenantId, Guid articleId, CancellationToken cancellationToken = default)
    {
        var article = await GetArticleOrThrowAsync(tenantId, articleId, cancellationToken);

        var tagCount = await db.KbItemTags.CountAsync(t => t.TenantId == tenantId && t.KbRefKind == "Article" && t.KbRefId == articleId, cancellationToken);

        // Snapshot the pre-publish content (this is version N; Publish() below bumps Version to
        // N+1 for whatever comes next) before mutating the article itself.
        var snapshot = new KbArticleVersion(tenantId, articleId, article.Version, article.Title, article.Body, article.AuthorId);

        try
        {
            article.Publish(tagCount > 0);
        }
        catch (InvalidOperationException ex) when (article.ReviewerId is null || article.ReviewerId == article.AuthorId)
        {
            throw new DomainRuleException("SELF_REVIEW_NOT_ALLOWED", ex.Message);
        }

        try
        {
            await db.KbArticleVersions.AddAsync(snapshot, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsSelfReviewTriggerError(ex))
        {
            throw new DomainRuleException("SELF_REVIEW_NOT_ALLOWED", $"Article {articleId}'s reviewer must differ from its author (Module 12 peer-review rule).");
        }

        var tagNames = await db.KbItemTags.Where(t => t.TenantId == tenantId && t.KbRefKind == "Article" && t.KbRefId == articleId)
            .Join(db.KbTags, it => it.TagId, t => t.Id, (it, t) => t.Name)
            .ToListAsync(cancellationToken);

        // AC-KB5: only Publish indexes an article — Draft/InReview content never reaches search.
        await searchIndexer.IndexAsync(tenantId, new KbSearchDoc("Article", article.Id, article.Title, article.Body, null, null, null, tagNames, null), cancellationToken);

        return ToDto(article);
    }

    public async Task<KbArticleDto?> GetAsync(Guid tenantId, Guid articleId, Guid callerId, bool callerCanReview, CancellationToken cancellationToken = default)
    {
        var article = await db.KbArticles.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.Id == articleId, cancellationToken);
        if (article is null || !IsVisible(article, callerId, callerCanReview))
        {
            return null;
        }

        return ToDto(article);
    }

    public async Task<IReadOnlyList<KbArticleDto>> GetVisibleAsync(Guid tenantId, Guid callerId, bool callerCanReview, CancellationToken cancellationToken = default)
    {
        var articles = await db.KbArticles.Where(a => a.TenantId == tenantId).ToListAsync(cancellationToken);
        return articles.Where(a => IsVisible(a, callerId, callerCanReview)).Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<KbArticleVersionDto>> GetVersionsAsync(Guid tenantId, Guid articleId, CancellationToken cancellationToken = default)
    {
        var versions = await db.KbArticleVersions.Where(v => v.TenantId == tenantId && v.ArticleId == articleId).OrderByDescending(v => v.VersionNo).ToListAsync(cancellationToken);
        return versions.Select(v => new KbArticleVersionDto(v.Id, v.VersionNo, v.Title, v.Body, v.AuthorId, v.CreatedAt)).ToList();
    }

    /// <summary>AC-KB5: "unreviewed article never visible to non-authors." Published articles are firm-wide readable by default (Module 12 Security Rules).</summary>
    private static bool IsVisible(KbArticle article, Guid callerId, bool callerCanReview)
        => article.Status == "Published" || article.AuthorId == callerId || callerCanReview;

    private async Task<KbArticle> GetArticleOrThrowAsync(Guid tenantId, Guid articleId, CancellationToken cancellationToken)
        => await db.KbArticles.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.Id == articleId, cancellationToken)
           ?? throw new NotFoundException(nameof(KbArticle), articleId);

    private static bool IsSelfReviewTriggerError(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("reviewer must differ from author", StringComparison.OrdinalIgnoreCase) == true;

    private static KbArticleDto ToDto(KbArticle a) => new(a.Id, a.Title, a.Body, a.Status, a.Version, a.AuthorId, a.ReviewerId, a.PublishedAt);
}
