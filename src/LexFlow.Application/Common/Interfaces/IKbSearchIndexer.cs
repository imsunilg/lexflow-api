namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Elasticsearch index for KB content (Acts/Sections/Judgments/Articles combined into one "kind"-
/// discriminated index, per Module 12's "dedicated KB search: filters by type, court, year, act").
/// Behind an interface so it's mockable in tests — no test in this codebase should hit a real
/// Elasticsearch cluster. AC-KB5: Draft/InReview articles are never indexed (see
/// KbArticleService — only Publish calls IndexAsync for an article), so an unpublished article can
/// never surface in search regardless of query.
/// </summary>
public interface IKbSearchIndexer
{
    Task IndexAsync(Guid tenantId, KbSearchDoc doc, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, string kind, Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KbSearchHit>> SearchAsync(Guid tenantId, KbSearchQuery query, CancellationToken cancellationToken = default);
}

/// <summary>Kind is one of "Act", "ActSection", "Judgment", "Article" (Template is DMS-indexed, not duplicated here).</summary>
public sealed record KbSearchDoc(string Kind, Guid Id, string Title, string? Text, Guid? CourtId, int? Year, Guid? ActId, IReadOnlyList<string> Tags, string? Citation);

public sealed record KbSearchQuery(string? Text, string? Type, Guid? CourtId, int? YearFrom, string? Tag);

public sealed record KbSearchHit(string Kind, Guid Id, string Title, double Score, string? Snippet);
