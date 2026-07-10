using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Kb;

/// <summary>
/// Module 12 User Flow #4 + AC-KB1/AC-KB2: citation-format-aware query parsing in front of the
/// ES-backed general search. A recognized section lookup ("IPC 420") resolves directly against
/// Postgres — no ES round trip — to hit AC-KB1's &lt; 500 ms budget; a recognized citation format
/// searches kb_judgments by citation/neutral_citation first; anything else is a general ES query
/// across the kb-items index, flagged IsFreeformFallback per Module 12's own "free-form fallback
/// flag" validation rule.
/// </summary>
public sealed class KbSearchService(LexFlowDbContext db, IKbActService actService, IKbSearchIndexer searchIndexer) : IKbSearchService
{
    public async Task<KbSearchResult> SearchAsync(Guid tenantId, string query, string? type, Guid? courtId, int? yearFrom, string? tag, CancellationToken cancellationToken = default)
    {
        var parsed = KbCitationQueryParser.Parse(query);

        if (parsed.Kind == KbQueryKind.SectionLookup && parsed.ActRef is not null && parsed.SectionNumber is not null)
        {
            var section = await actService.LookupSectionAsync(tenantId, parsed.ActRef, parsed.SectionNumber, cancellationToken);
            if (section is not null)
            {
                return new KbSearchResult(section, null, [], false);
            }
        }

        if (parsed.Kind == KbQueryKind.Citation)
        {
            var judgment = await db.KbJudgments
                .Where(j => j.TenantId == tenantId && (j.Citation == parsed.RawQuery || j.NeutralCitation == parsed.RawQuery))
                .SingleOrDefaultAsync(cancellationToken);

            if (judgment is not null)
            {
                return new KbSearchResult(null, new KbJudgmentDto(judgment.Id, judgment.Citation, judgment.NeutralCitation, judgment.CourtId, judgment.DecisionDate, judgment.Parties, judgment.Headnote, judgment.DocumentId, "NotApplicable"), [], false);
            }

            // Error Handling: "broken citation lookup -> suggestions via fuzzy" — falls through to
            // the general ES query below, which naturally fuzzy-matches on the raw citation text.
        }

        var hits = await searchIndexer.SearchAsync(tenantId, new KbSearchQuery(query, type, courtId, yearFrom, tag), cancellationToken);
        return new KbSearchResult(null, null, hits, parsed.Kind == KbQueryKind.Freeform);
    }
}
