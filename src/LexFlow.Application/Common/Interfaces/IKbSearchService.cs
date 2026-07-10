namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 12 User Flow #4: "dedicated KB search (ES): filters by type, court, year, act;
/// section-number direct jump ('IPC 420' -&gt; section); citation-format search ('(2023) 5 SCC
/// 1')." AC-KB1: a recognized section lookup is resolved directly against Postgres (cached),
/// bypassing ES entirely, to hit the &lt; 500 ms budget. A recognized citation searches
/// kb_judgments by citation/neutral_citation first; anything else falls through to a general ES
/// query across the kb-items index.
/// </summary>
public interface IKbSearchService
{
    Task<KbSearchResult> SearchAsync(Guid tenantId, string query, string? type, Guid? courtId, int? yearFrom, string? tag, CancellationToken cancellationToken = default);
}

/// <summary>Exactly one of DirectSection/DirectJudgment is set when the query parser recognized a direct-lookup pattern; Hits carries the general-search results otherwise (or alongside, for a citation match that isn't exact).</summary>
public sealed record KbSearchResult(KbActSectionDto? DirectSection, KbJudgmentDto? DirectJudgment, IReadOnlyList<KbSearchHit> Hits, bool IsFreeformFallback);
