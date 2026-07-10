using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LexFlow.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace LexFlow.Infrastructure.Ai;

/// <summary>
/// Module 16 Architecture: "AI Gateway service wraps LLM providers (Anthropic Claude primary;
/// provider-pluggable) via server-side calls only; no client-side model keys." Calls the
/// Anthropic Messages API directly over HTTP (no first-party SDK dependency, mirroring this
/// codebase's own-HTTP-call pattern for Stripe/Razorpay/PayPal). §35 Safety: "refusal passthrough
/// (provider safety declines surfaced honestly)" — a stop_reason of "refusal" maps to
/// <see cref="LlmCompletionResult.Refused"/> rather than throwing.
/// </summary>
public sealed class AnthropicLlmProvider(IHttpClientFactory httpClientFactory, IOptions<AiOptions> options) : ILlmProvider
{
    public string ProviderName => "anthropic";

    public async Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.AnthropicApiKey))
        {
            throw new InvalidOperationException("Ai:AnthropicApiKey is not configured — the AI Gateway cannot call the provider without server-side credentials (Module 16: 'no client-side model keys').");
        }

        var client = httpClientFactory.CreateClient(nameof(AnthropicLlmProvider));
        var payload = new AnthropicRequest(
            request.Model,
            request.MaxTokens,
            request.Temperature,
            request.SystemPrompt,
            request.Messages.Select(m => new AnthropicMessage(m.Role, m.Content)).ToList());

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, config.AnthropicBaseUrl)
        {
            Content = JsonContent.Create(payload),
        };
        httpRequest.Headers.Add("x-api-key", config.AnthropicApiKey);
        httpRequest.Headers.Add("anthropic-version", config.AnthropicApiVersion);

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var parsed = JsonSerializer.Deserialize<AnthropicResponse>(body) ?? throw new InvalidOperationException("Anthropic API returned an unparseable response.");
        var text = string.Join(string.Empty, parsed.Content.Select(c => c.Text));
        var refused = parsed.StopReason == "refusal";

        return new LlmCompletionResult(text, parsed.Usage.InputTokens, parsed.Usage.OutputTokens, request.Model, refused);
    }

    private sealed record AnthropicRequest(
        string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        double Temperature,
        [property: JsonPropertyName("system")] string? SystemPrompt,
        IReadOnlyList<AnthropicMessage> Messages);

    private sealed record AnthropicMessage(string Role, string Content);

    private sealed record AnthropicResponse(
        [property: JsonPropertyName("content")] IReadOnlyList<AnthropicContentBlock> Content,
        [property: JsonPropertyName("stop_reason")] string? StopReason,
        [property: JsonPropertyName("usage")] AnthropicUsage Usage);

    private sealed record AnthropicContentBlock(string Text);

    private sealed record AnthropicUsage(
        [property: JsonPropertyName("input_tokens")] int InputTokens,
        [property: JsonPropertyName("output_tokens")] int OutputTokens);
}
