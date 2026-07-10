namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 12: internal articles, draft -&gt; peer review -&gt; publish, versioned. BR (peer-review
/// rule): "article publish requires ≥1 tag + reviewer ≠ author" is enforced first at the DB
/// (kb.kb_articles 004_Triggers.sql) and again here in <see cref="PublishAsync"/> before the
/// doomed UPDATE is ever attempted — defense in depth, this module's own build brief. AC-KB5:
/// "unreviewed article never visible to non-authors" is enforced in every read path via the
/// callerId/callerCanReview parameters, not left to the caller to filter.
/// </summary>
public interface IKbArticleService
{
    Task<KbArticleDto> CreateDraftAsync(Guid tenantId, Guid authorId, string title, string? body, CancellationToken cancellationToken = default);

    Task<KbArticleDto> UpdateDraftAsync(Guid tenantId, Guid articleId, string title, string? body, CancellationToken cancellationToken = default);

    Task<KbArticleDto> SubmitForReviewAsync(Guid tenantId, Guid articleId, CancellationToken cancellationToken = default);

    Task<KbArticleDto> AssignReviewerAsync(Guid tenantId, Guid articleId, Guid reviewerId, CancellationToken cancellationToken = default);

    Task<KbArticleDto> SendBackToDraftAsync(Guid tenantId, Guid articleId, CancellationToken cancellationToken = default);

    /// <summary>Snapshots the pre-publish content into kb_article_versions, then transitions to Published — the DB trigger's reviewer≠author check is the final backstop even if this method's own pre-check were somehow bypassed.</summary>
    Task<KbArticleDto> PublishAsync(Guid tenantId, Guid articleId, CancellationToken cancellationToken = default);

    /// <summary>AC-KB5: returns null (not the article) when the caller is neither the author nor holds kb.review, and the article isn't Published.</summary>
    Task<KbArticleDto?> GetAsync(Guid tenantId, Guid articleId, Guid callerId, bool callerCanReview, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KbArticleDto>> GetVisibleAsync(Guid tenantId, Guid callerId, bool callerCanReview, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KbArticleVersionDto>> GetVersionsAsync(Guid tenantId, Guid articleId, CancellationToken cancellationToken = default);
}

public sealed record KbArticleDto(Guid Id, string Title, string? Body, string Status, int Version, Guid? AuthorId, Guid? ReviewerId, DateTimeOffset? PublishedAt);

public sealed record KbArticleVersionDto(Guid Id, int VersionNo, string? Title, string? Body, Guid? AuthorId, DateTimeOffset CreatedAt);
