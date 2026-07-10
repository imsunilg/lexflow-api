using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch;
using LexFlow.Application.Common.Interfaces;

namespace LexFlow.Infrastructure.Kb;

/// <summary>GET /api/v1/kb/search — Module 12's single ES index across Acts/Sections/Judgments/Articles, discriminated by "kind". See ElasticsearchDocumentIndexer (Module 7) for the sibling pattern this mirrors.</summary>
public sealed class ElasticsearchKbIndexer(ElasticsearchClient elasticsearchClient) : IKbSearchIndexer
{
    private const string IndexName = "kb-items";

    public async Task IndexAsync(Guid tenantId, KbSearchDoc doc, CancellationToken cancellationToken = default)
    {
        var indexDoc = new KbIndexDoc(
            tenantId.ToString(), doc.Kind, doc.Title, doc.Text, doc.CourtId?.ToString(), doc.Year, doc.ActId?.ToString(), doc.Tags, doc.Citation);

        await elasticsearchClient.IndexAsync(indexDoc, idx => idx.Index(IndexName).Id($"{doc.Kind}:{doc.Id}"), cancellationToken);
    }

    public async Task DeleteAsync(Guid tenantId, string kind, Guid id, CancellationToken cancellationToken = default)
        => await elasticsearchClient.DeleteAsync<KbIndexDoc>($"{kind}:{id}", d => d.Index(IndexName), cancellationToken);

    public async Task<IReadOnlyList<KbSearchHit>> SearchAsync(Guid tenantId, KbSearchQuery query, CancellationToken cancellationToken = default)
    {
        var response = await elasticsearchClient.SearchAsync<KbIndexDoc>(s =>
        {
            s.Indices(IndexName).Query(q => q.Bool(b =>
            {
                b.Filter(f => f.Term(t => t.Field(d => d.TenantId).Value(tenantId.ToString())));

                if (!string.IsNullOrWhiteSpace(query.Text))
                {
                    b.Must(m => m.MultiMatch(mm => mm.Fields(new[] { "title", "text", "citation" }).Query(query.Text)));
                }

                if (!string.IsNullOrWhiteSpace(query.Type))
                {
                    b.Filter(f => f.Term(t => t.Field(d => d.Kind).Value(query.Type)));
                }

                if (query.CourtId.HasValue)
                {
                    b.Filter(f => f.Term(t => t.Field(d => d.CourtId).Value(query.CourtId.Value.ToString())));
                }

                if (query.YearFrom.HasValue)
                {
                    b.Filter(f => f.Range(r => r.Number(n => n.Field(d => d.Year).Gte(query.YearFrom.Value))));
                }

                if (!string.IsNullOrWhiteSpace(query.Tag))
                {
                    b.Filter(f => f.Term(t => t.Field(d => d.Tags).Value(query.Tag)));
                }
            }));
        }, cancellationToken);

        if (!response.IsValidResponse)
        {
            return [];
        }

        return response.Hits
            .Select(h => new KbSearchHit(h.Source!.Kind, Guid.Parse(h.Id!.Split(':')[1]), h.Source.Title, h.Score ?? 0, null))
            .ToList();
    }

    private sealed record KbIndexDoc(
        [property: JsonPropertyName("tenantId")] string TenantId,
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("courtId")] string? CourtId,
        [property: JsonPropertyName("year")] int? Year,
        [property: JsonPropertyName("actId")] string? ActId,
        [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
        [property: JsonPropertyName("citation")] string? Citation);
}
