namespace LexFlow.Infrastructure.Ai;

/// <summary>Module 16 Architecture: "AI Gateway service wraps LLM providers (Anthropic Claude primary) via server-side calls only; no client-side model keys." Bound from configuration section "Ai".</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string? AnthropicApiKey { get; set; }

    public string AnthropicBaseUrl { get; set; } = "https://api.anthropic.com/v1/messages";

    public string AnthropicApiVersion { get; set; } = "2023-06-01";

    /// <summary>Module 16 Architecture: "per-tenant monthly AI-credit quota by tier" — default ceiling for a tenant with no explicit ai.ai_tenant_quotas row yet.</summary>
    public decimal DefaultMonthlyCreditLimit { get; set; } = 1000;
}
