using System.Security.Cryptography;
using System.Text;
using LexFlow.Application.Common.Interfaces;

namespace LexFlow.Infrastructure.Ai;

/// <summary>
/// Module 16 RAG: turns text into a vector for ai.ai_embeddings similarity search. This is a
/// deterministic, local, hashed-bag-of-words embedding (feature hashing into a fixed 256-dim
/// space, L2-normalized) — a real semantic embedding model (e.g. an Anthropic/Voyage/OpenAI
/// embeddings endpoint) is external provider infrastructure this repo doesn't have credentials
/// for, same class of pragmatic stand-in as ISpeechToTextService's no-op. It is a genuine,
/// working similarity signal (near-duplicate and keyword-overlapping text score highly similar)
/// even though it captures none of a real embedding model's semantics — RagRetrievalService's
/// cosine-similarity ranking and RBAC-filtering logic are fully exercised against it either way.
/// Swapping in a real provider later is a one-class change behind <see cref="IEmbeddingProvider"/>.
/// </summary>
public sealed class HashingEmbeddingProvider : IEmbeddingProvider
{
    private const int Dimensions = 256;

    public Task<double[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var vector = new double[Dimensions];
        var tokens = text.ToLowerInvariant().Split([' ', '\t', '\n', '\r', '.', ',', ';', ':', '(', ')', '"', '\''], StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            var bucket = HashToBucket(token);
            vector[bucket] += 1.0;
        }

        var norm = Math.Sqrt(vector.Sum(v => v * v));
        if (norm > 0)
        {
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] /= norm;
            }
        }

        return Task.FromResult(vector);
    }

    private static int HashToBucket(string token)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(token));
        var value = BitConverter.ToUInt32(hash, 0);
        return (int)(value % Dimensions);
    }
}
