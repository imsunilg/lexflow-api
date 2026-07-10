using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ai.ai_embeddings (lexflow-database Scripts/18_AI/AiEmbeddings).
/// Module 16 RAG chunk store. Polymorphic source (SourceKind/SourceId), one row per chunk of a
/// source document/matter/KB item's text. <see cref="Embedding"/> is a plain array (this
/// environment's Postgres has no pgvector extension available — see the table's own
/// 001_Table.sql comment) rather than a true vector column; RagRetrievalService computes cosine
/// similarity over it in application code.
/// </summary>
public sealed class AiEmbedding : Entity
{
    private AiEmbedding()
    {
    }

    public AiEmbedding(Guid tenantId, string sourceKind, Guid sourceId, int chunkIndex, string chunkText, double[]? embedding, string metadataJson)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        SourceKind = sourceKind;
        SourceId = sourceId;
        ChunkIndex = chunkIndex;
        ChunkText = chunkText;
        Embedding = embedding;
        MetadataJson = metadataJson;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public string SourceKind { get; private set; } = null!;
    public Guid SourceId { get; private set; }
    public int ChunkIndex { get; private set; }
    public string ChunkText { get; private set; } = null!;
    public double[]? Embedding { get; private set; }
    public string MetadataJson { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void SetEmbedding(double[] embedding)
    {
        Embedding = embedding;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateChunk(string chunkText, string metadataJson)
    {
        ChunkText = chunkText;
        MetadataJson = metadataJson;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
