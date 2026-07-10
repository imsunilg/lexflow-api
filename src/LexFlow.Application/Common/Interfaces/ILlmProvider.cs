namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 16 Architecture: "AI Gateway service (LexFlow.AI) wraps LLM providers (Anthropic
/// Claude primary; provider-pluggable) via server-side calls only; no client-side model keys."
/// Every AI feature goes through this abstraction rather than calling a provider SDK directly,
/// so the provider is swappable per §35's "model/provider switch per feature via config"
/// without touching feature code.
/// </summary>
public interface ILlmProvider
{
    string ProviderName { get; }

    Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default);
}

public sealed record LlmCompletionRequest(string Model, string? SystemPrompt, IReadOnlyList<LlmMessage> Messages, int MaxTokens, double Temperature);

public sealed record LlmMessage(string Role, string Content);

/// <summary><see cref="Refused"/> surfaces a provider safety decline honestly rather than as a generic failure (§35 Safety: "refusal passthrough").</summary>
public sealed record LlmCompletionResult(string Text, int TokensInput, int TokensOutput, string Model, bool Refused = false);
