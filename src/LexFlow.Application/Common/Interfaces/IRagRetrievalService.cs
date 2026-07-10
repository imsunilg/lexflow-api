namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 16 RAG: "tenant-scoped vector store (pgvector) over documents (OCR text), matters, KB;
/// retrieval always filtered by caller's RBAC scope before context assembly." Every candidate
/// chunk is re-checked against <see cref="IAiRetrievalGuard"/> for the specific caller *after*
/// the similarity search and *before* it's assembled into an LLM prompt — a chunk the caller
/// cannot read is dropped silently (AC-AI1: "assistant answers 'no accessible records' — never
/// leaks existence"), not surfaced as a redacted stub.
/// </summary>
public interface IRagRetrievalService
{
    Task<IReadOnlyList<RagChunk>> RetrieveAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, string query, int topK, IReadOnlyCollection<string>? sourceKinds = null, CancellationToken cancellationToken = default);

    /// <summary>(Re)computes and persists the chunk embeddings for one source record — called after a document's text is extracted, a matter is updated, or a KB item is published.</summary>
    Task IndexAsync(Guid tenantId, string sourceKind, Guid sourceId, string text, CancellationToken cancellationToken = default);
}

public sealed record RagChunk(string SourceKind, Guid SourceId, string ChunkText, double Score);
