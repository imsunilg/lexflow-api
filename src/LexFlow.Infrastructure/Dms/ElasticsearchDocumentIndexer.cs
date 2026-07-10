using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using LexFlow.Application.Common.Interfaces;

namespace LexFlow.Infrastructure.Dms;

/// <summary>
/// GET /api/v1/documents/search — ES-backed, OCR text included. AC-DOC3: privileged
/// gating happens inside <see cref="SearchAsync"/> itself (a Bool "must_not" filter on
/// confidentiality=Privileged unless the caller has document.privileged.read), so a
/// privileged document is invisible in results, not merely blocked on open.
/// </summary>
public sealed class ElasticsearchDocumentIndexer(ElasticsearchClient elasticsearchClient) : IDocumentIndexer
{
    private const string IndexName = "dms-documents";

    public async Task IndexAsync(Guid tenantId, Guid documentId, DocumentIndexPayload payload, CancellationToken cancellationToken = default)
    {
        var doc = new DocumentIndexDoc(
            tenantId.ToString(), payload.Title, payload.DocType, payload.Confidentiality,
            payload.MatterId, payload.ClientId, payload.CaseId, payload.ExtractedText, payload.Tags);

        await elasticsearchClient.IndexAsync(doc, idx => idx.Index(IndexName).Id(documentId.ToString()), cancellationToken);
    }

    public async Task DeleteAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken = default)
        => await elasticsearchClient.DeleteAsync<DocumentIndexDoc>(documentId.ToString(), d => d.Index(IndexName), cancellationToken);

    public async Task<IReadOnlyList<DocumentSearchHit>> SearchAsync(Guid tenantId, DocumentSearchQuery query, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default)
    {
        var canSeePrivileged = callerPermissions.Contains("document.privileged.read");

        var response = await elasticsearchClient.SearchAsync<DocumentIndexDoc>(s =>
        {
            s.Indices(IndexName).Query(q => q.Bool(b =>
            {
                b.Filter(f => f.Term(t => t.Field(d => d.TenantId).Value(tenantId.ToString())));

                if (!canSeePrivileged)
                {
                    b.MustNot(mn => mn.Term(t => t.Field(d => d.Confidentiality).Value("Privileged")));
                }

                if (!string.IsNullOrWhiteSpace(query.Text))
                {
                    b.Must(m => m.MultiMatch(mm => mm.Fields(new[] { "title", "extractedText" }).Query(query.Text)));
                }

                if (query.MatterId.HasValue)
                {
                    b.Filter(f => f.Term(t => t.Field(d => d.MatterId).Value(query.MatterId.Value.ToString())));
                }

                if (!string.IsNullOrWhiteSpace(query.DocType))
                {
                    b.Filter(f => f.Term(t => t.Field(d => d.DocType).Value(query.DocType)));
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
            .Select(h => new DocumentSearchHit(Guid.Parse(h.Id!), h.Source!.Title, h.Source.DocType, h.Source.Confidentiality, h.Score ?? 0, null))
            .ToList();
    }

    private sealed record DocumentIndexDoc(
        [property: JsonPropertyName("tenantId")] string TenantId,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("docType")] string DocType,
        [property: JsonPropertyName("confidentiality")] string Confidentiality,
        [property: JsonPropertyName("matterId")] Guid? MatterId,
        [property: JsonPropertyName("clientId")] Guid? ClientId,
        [property: JsonPropertyName("caseId")] Guid? CaseId,
        [property: JsonPropertyName("extractedText")] string? ExtractedText,
        [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags);
}
