namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 16 Architecture: "Every AI action logged (ai_interactions: feature, prompt-template id, model, tokens, latency, user, target refs, feedback) — prompts/outputs retained per firm policy (default 90 days)."</summary>
public interface IAiInteractionAuditService
{
    Task<Guid> RecordAsync(AiInteractionRecord record, CancellationToken cancellationToken = default);

    Task RecordFeedbackAsync(Guid tenantId, Guid interactionId, int rating, string? reason, CancellationToken cancellationToken = default);
}

public sealed record AiInteractionRecord(
    Guid TenantId,
    string Feature,
    string PromptTemplateKey,
    string PromptTemplateVersion,
    string Model,
    int TokensInput,
    int TokensOutput,
    int LatencyMs,
    decimal CreditsCharged,
    Guid? UserId,
    string? TargetRefKind,
    Guid? TargetRefId,
    string? InputText,
    string? OutputText);
