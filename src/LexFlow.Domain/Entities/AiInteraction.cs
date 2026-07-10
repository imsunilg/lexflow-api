using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ai.ai_interactions (lexflow-database Scripts/18_AI/AiInteractions).
/// Module 16 Architecture: "Every AI action logged (ai_interactions: feature, prompt-template id,
/// model, tokens, latency, user, target refs, feedback)." Append-only audit row per AI call — no
/// mutator beyond <see cref="RecordFeedback"/> (thumbs + reason, §35 feedback loop). Not an
/// AuditableEntity: this table has no created_by/updated_by/is_deleted columns — it is itself the
/// audit trail, not a business record needing one.
/// </summary>
public sealed class AiInteraction : Entity
{
    private AiInteraction()
    {
    }

    public AiInteraction(
        Guid tenantId,
        string feature,
        string promptTemplateKey,
        string promptTemplateVersion,
        string model,
        int tokensInput,
        int tokensOutput,
        int latencyMs,
        decimal creditsCharged,
        Guid? userId,
        string? targetRefKind,
        Guid? targetRefId,
        string? inputText,
        string? outputText,
        DateTimeOffset? retentionExpiresAt)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Feature = feature;
        PromptTemplateKey = promptTemplateKey;
        PromptTemplateVersion = promptTemplateVersion;
        Model = model;
        TokensInput = tokensInput;
        TokensOutput = tokensOutput;
        LatencyMs = latencyMs;
        CreditsCharged = creditsCharged;
        UserId = userId;
        TargetRefKind = targetRefKind;
        TargetRefId = targetRefId;
        InputText = inputText;
        OutputText = outputText;
        CreatedAt = DateTimeOffset.UtcNow;
        RetentionExpiresAt = retentionExpiresAt;
    }

    public Guid TenantId { get; private set; }
    public string Feature { get; private set; } = null!;
    public string PromptTemplateKey { get; private set; } = null!;
    public string PromptTemplateVersion { get; private set; } = null!;
    public string Model { get; private set; } = null!;
    public int TokensInput { get; private set; }
    public int TokensOutput { get; private set; }
    public int LatencyMs { get; private set; }
    public decimal CreditsCharged { get; private set; }
    public Guid? UserId { get; private set; }
    public string? TargetRefKind { get; private set; }
    public Guid? TargetRefId { get; private set; }
    public string? InputText { get; private set; }
    public string? OutputText { get; private set; }
    public int? Rating { get; private set; }
    public string? RatingReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RetentionExpiresAt { get; private set; }

    /// <summary>§35 feedback loop: "thumbs + reason on every output -> weekly eval dashboard." rating is +1 (up) or -1 (down), matching the DB CHECK constraint.</summary>
    public void RecordFeedback(int rating, string? reason)
    {
        if (rating is not (1 or -1))
        {
            throw new InvalidOperationException("Rating must be +1 (thumbs up) or -1 (thumbs down).");
        }

        Rating = rating;
        RatingReason = reason;
    }
}
