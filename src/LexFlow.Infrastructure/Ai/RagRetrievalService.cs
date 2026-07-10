using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Ai;

/// <summary>See IRagRetrievalService's own doc comment. Candidate chunks are scanned newest-first, capped at <see cref="CandidateScanCap"/> per call, to bound cost without a true ANN index (see ai.ai_embeddings/002_Indexes.sql for why).</summary>
public sealed class RagRetrievalService(LexFlowDbContext db, IEmbeddingProvider embeddingProvider, IAiRetrievalGuard retrievalGuard) : IRagRetrievalService
{
    private const int ChunkSize = 1000;
    private const int CandidateScanCap = 500;

    public async Task<IReadOnlyList<RagChunk>> RetrieveAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, string query, int topK, IReadOnlyCollection<string>? sourceKinds = null, CancellationToken cancellationToken = default)
    {
        var queryVector = await embeddingProvider.EmbedAsync(query, cancellationToken);

        var candidatesQuery = db.AiEmbeddings.Where(e => e.TenantId == tenantId && e.Embedding != null);
        if (sourceKinds is { Count: > 0 })
        {
            candidatesQuery = candidatesQuery.Where(e => sourceKinds.Contains(e.SourceKind));
        }

        var candidates = await candidatesQuery
            .OrderByDescending(e => e.CreatedAt)
            .Take(CandidateScanCap)
            .ToListAsync(cancellationToken);

        var ranked = candidates
            .Select(c => (Chunk: c, Score: CosineSimilarity(queryVector, c.Embedding!)))
            .OrderByDescending(x => x.Score)
            .ToList();

        var result = new List<RagChunk>();
        foreach (var (chunk, score) in ranked)
        {
            if (result.Count >= topK)
            {
                break;
            }

            // RBAC-filtered retrieval: a chunk the caller cannot read is dropped silently, never
            // surfaced even as a redacted placeholder (Module 16 Security / AC-AI1).
            if (!await retrievalGuard.CanAccessAsync(tenantId, callerId, callerPermissions, chunk.SourceKind, chunk.SourceId, cancellationToken))
            {
                continue;
            }

            result.Add(new RagChunk(chunk.SourceKind, chunk.SourceId, chunk.ChunkText, score));
        }

        return result;
    }

    public async Task IndexAsync(Guid tenantId, string sourceKind, Guid sourceId, string text, CancellationToken cancellationToken = default)
    {
        var existing = await db.AiEmbeddings.Where(e => e.TenantId == tenantId && e.SourceKind == sourceKind && e.SourceId == sourceId).ToListAsync(cancellationToken);
        db.AiEmbeddings.RemoveRange(existing);

        var chunks = Chunk(text);
        for (var i = 0; i < chunks.Count; i++)
        {
            var embedding = await embeddingProvider.EmbedAsync(chunks[i], cancellationToken);
            await db.AiEmbeddings.AddAsync(new AiEmbedding(tenantId, sourceKind, sourceId, i, chunks[i], embedding, "{}"), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static List<string> Chunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var chunks = new List<string>();
        for (var offset = 0; offset < text.Length; offset += ChunkSize)
        {
            chunks.Add(text.Substring(offset, Math.Min(ChunkSize, text.Length - offset)));
        }

        return chunks;
    }

    private static double CosineSimilarity(double[] a, double[] b)
    {
        var length = Math.Min(a.Length, b.Length);
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
