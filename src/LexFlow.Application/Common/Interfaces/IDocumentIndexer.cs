namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Elasticsearch document index — the consumer side of the transactional-outbox
/// pattern (see DocumentIndexOutbox entity / DocumentIndexDispatchService). Behind
/// an interface so it's mockable in tests — no test in this codebase should hit a
/// real Elasticsearch cluster.
/// </summary>
public interface IDocumentIndexer
{
    Task IndexAsync(Guid tenantId, Guid documentId, DocumentIndexPayload payload, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>AC-DOC3: confidentiality/privileged gating happens here — callerPermissions determines which confidentiality levels are visible, so a privileged doc is invisible to a search without document.privileged.read, not merely blocked on open.</summary>
    Task<IReadOnlyList<DocumentSearchHit>> SearchAsync(Guid tenantId, DocumentSearchQuery query, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken = default);
}

public sealed record DocumentIndexPayload(string Title, string DocType, string Confidentiality, Guid? MatterId, Guid? ClientId, Guid? CaseId, string? ExtractedText, IReadOnlyList<string> Tags);

public sealed record DocumentSearchQuery(string? Text, Guid? MatterId, string? DocType, string? Tag, DateTimeOffset? From, DateTimeOffset? To);

public sealed record DocumentSearchHit(Guid DocumentId, string Title, string DocType, string Confidentiality, double Score, string? Snippet);
