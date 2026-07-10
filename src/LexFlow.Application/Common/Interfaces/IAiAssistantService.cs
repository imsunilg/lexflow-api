namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 16 features 1-9 and 12 (chat, document/case summary, contract review, drafting,
/// research, case recommendation, hearing prediction, risk analysis, email generation). Features
/// 10/11 (voice notes, meeting summary) are <see cref="IAiTranscriptionService"/> plus
/// <see cref="SummarizeMeetingAsync"/> here. Every method threads <paramref name="callerId"/>/
/// <paramref name="callerPermissions"/> through to <see cref="IRagRetrievalService"/> so context
/// assembly is always RBAC-filtered (Module 16 Security).
/// </summary>
public interface IAiAssistantService
{
    Task<AiChatResponse> ChatAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, AiChatRequest request, CancellationToken cancellationToken = default);

    Task<AiDocumentSummaryResponse> SummarizeDocumentAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, Guid documentId, string? lengthPreset, CancellationToken cancellationToken = default);

    Task<AiMatterSummaryResponse> SummarizeMatterAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, Guid matterId, CancellationToken cancellationToken = default);

    Task<AiContractReviewResponse> ReviewContractAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, Guid documentId, CancellationToken cancellationToken = default);

    Task<AiDraftResponse> DraftAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, AiDraftRequest request, CancellationToken cancellationToken = default);

    Task<AiResearchResponse> ResearchAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, string question, bool webGroundedMode, CancellationToken cancellationToken = default);

    Task<AiSimilarMattersResponse> GetSimilarMattersAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, Guid matterId, int topN, CancellationToken cancellationToken = default);

    Task<AiHearingPredictionResponse> PredictNextHearingAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, Guid caseId, CancellationToken cancellationToken = default);

    Task<AiRiskAnalysisResponse> GetRiskAnalysisAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, Guid matterId, CancellationToken cancellationToken = default);

    Task<AiEmailGenerateResponse> GenerateEmailAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, AiEmailGenerateRequest request, CancellationToken cancellationToken = default);

    Task<AiMeetingSummaryResponse> SummarizeMeetingAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, Guid transcriptionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// BR-19/AC-AI5: "Every AI output rendered with AI-badge and requires explicit Save/Insert
/// action." IsAiGenerated has no setter (not even `init`) — a derived record cannot override it,
/// so it is structurally impossible for any AI response DTO in this codebase to report itself as
/// anything other than AI-generated. See AiGeneratedResponseTests for the enforcement test.
/// </summary>
public abstract record AiGeneratedResponse
{
    public bool IsAiGenerated { get; } = true;

    public string Disclaimer { get; } = "AI-generated — review required. A human action (save/insert/send) is always required before this becomes part of the record.";
}

public sealed record AiChatTurn(string Role, string Content);

public sealed record AiChatRequest(string Message, string? Command, Guid? MatterId, Guid? DocumentId, IReadOnlyList<AiChatTurn>? History);

public sealed record AiChatResponse(Guid InteractionId, string Text, IReadOnlyList<AiCitation> Citations) : AiGeneratedResponse;

public sealed record AiDocumentSummaryResponse(Guid InteractionId, string Summary, IReadOnlyList<string> KeyPoints) : AiGeneratedResponse;

public sealed record AiMatterSummaryResponse(Guid InteractionId, string Summary, IReadOnlyList<string> NextSteps) : AiGeneratedResponse;

public sealed record AiContractClause(string ClauseType, string? Text, string? RiskLevel);

public sealed record AiContractReviewResponse(Guid InteractionId, IReadOnlyList<AiContractClause> Clauses, IReadOnlyList<string> RiskFlags) : AiGeneratedResponse;

public sealed record AiDraftRequest(string Kind, Guid MatterId, IReadOnlyDictionary<string, string> IntakeFields);

public sealed record AiDraftResponse(Guid InteractionId, string DraftText) : AiGeneratedResponse;

public sealed record AiResearchResponse(Guid InteractionId, string Answer, IReadOnlyList<AiCitation> Citations, bool NoAuthorityFound) : AiGeneratedResponse;

public sealed record AiSimilarMatter(Guid MatterId, string Title, double Score);

public sealed record AiSimilarMattersResponse(Guid InteractionId, IReadOnlyList<AiSimilarMatter> Matters) : AiGeneratedResponse;

/// <summary>Module 16 feature #8: a heuristic stand-in for the PRD's "simple gradient-boosted model retrained weekly" (out of scope to train a real model here) — historical adjournment rate and hearing-gap statistics for the same court+case-type+stage. Edge Case: "prediction with &lt;50 historical samples -&gt; feature hidden 'insufficient data'" (<see cref="InsufficientData"/>).</summary>
public sealed record AiHearingPredictionResponse(Guid InteractionId, double AdjournmentProbability, int ExpectedGapDaysLow, int ExpectedGapDaysHigh, string Confidence, bool InsufficientData) : AiGeneratedResponse;

public sealed record AiRiskFactor(string Name, int Score, string Detail);

public sealed record AiRiskAnalysisResponse(Guid InteractionId, int RiskScore, IReadOnlyList<AiRiskFactor> Factors, string Rationale) : AiGeneratedResponse;

public sealed record AiEmailGenerateRequest(string Intent, string Tone, IReadOnlyList<string> BulletPoints, string? ThreadContext);

public sealed record AiEmailGenerateResponse(Guid InteractionId, string Subject, string Body) : AiGeneratedResponse;

public sealed record AiMeetingSummaryResponse(Guid InteractionId, string Summary, IReadOnlyList<string> Decisions, IReadOnlyList<string> ActionItems) : AiGeneratedResponse;
