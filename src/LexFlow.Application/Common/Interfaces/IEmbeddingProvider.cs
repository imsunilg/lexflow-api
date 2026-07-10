namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 16 RAG: turns source/query text into a dense vector for similarity search over ai.ai_embeddings. Kept independent from <see cref="ILlmProvider"/> since embedding and completion models are commonly different providers/endpoints even when the completion provider is fixed.</summary>
public interface IEmbeddingProvider
{
    Task<double[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
