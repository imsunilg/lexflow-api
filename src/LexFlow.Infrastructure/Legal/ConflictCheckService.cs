using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using LexFlow.Application.Common.Interfaces;

namespace LexFlow.Infrastructure.Legal;

/// <summary>
/// AC-M1 / FR-013 conflict-of-interest check: Elasticsearch fuzzy match over
/// legal.matter_parties + crm.clients party names (indexed here as they're created —
/// see <see cref="IndexPartyAsync"/>, called from MatterService/CourtCaseService).
/// Index auto-creates on first document via ES dynamic mapping; no separate index-setup
/// migration is needed for this best-effort search feature.
/// </summary>
public sealed class ConflictCheckService(ElasticsearchClient elasticsearchClient) : IConflictCheckService
{
    private const string IndexName = "legal-conflict-parties";
    private const double MatchThreshold = 0.6;

    public async Task<IReadOnlyList<ConflictMatch>> CheckAsync(Guid tenantId, IReadOnlyList<string> partyNames, CancellationToken cancellationToken = default)
    {
        var matches = new List<ConflictMatch>();

        foreach (var name in partyNames.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            var response = await elasticsearchClient.SearchAsync<PartyDocument>(s => s
                .Indices(IndexName)
                .Query(q => q.Bool(b => b
                    .Filter(f => f.Term(t => t.Field(d => d.TenantId).Value(tenantId.ToString())))
                    .Must(m => m.Match(mm => mm.Field(d => d.Name).Query(name).Fuzziness(new Fuzziness("AUTO")))))),
                cancellationToken);

            if (!response.IsValidResponse)
            {
                continue;
            }

            matches.AddRange(response.Hits
                .Where(h => (h.Score ?? 0) >= MatchThreshold)
                .Select(h => new ConflictMatch(h.Source!.Name, h.Score ?? 0, h.Source.SourceType, h.Source.SourceId, h.Source.MatterId, h.Source.MatterNumber)));
        }

        return matches;
    }

    public async Task IndexPartyAsync(Guid tenantId, string name, string sourceType, Guid sourceId, Guid? matterId, string? matterNumber, CancellationToken cancellationToken = default)
    {
        var document = new PartyDocument(tenantId.ToString(), name, sourceType, sourceId, matterId, matterNumber);
        await elasticsearchClient.IndexAsync(document, idx => idx.Index(IndexName).Id($"{sourceType}:{sourceId}"), cancellationToken);
    }

    private sealed record PartyDocument(
        [property: JsonPropertyName("tenantId")] string TenantId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("sourceType")] string SourceType,
        [property: JsonPropertyName("sourceId")] Guid SourceId,
        [property: JsonPropertyName("matterId")] Guid? MatterId,
        [property: JsonPropertyName("matterNumber")] string? MatterNumber);
}
