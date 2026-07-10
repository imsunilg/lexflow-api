using System.Diagnostics;
using System.Text.RegularExpressions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Ai;

/// <summary>
/// Module 16 features 1-9 and 12. Every method follows the same pipeline: check quota -&gt;
/// assemble RBAC-filtered context (RAG or a direct per-record fetch through the same guard) -&gt;
/// render the versioned prompt template -&gt; call ILlmProvider -&gt; verify any citations the model
/// echoed back -&gt; write the ai.ai_interactions audit row -&gt; return an AiGeneratedResponse DTO.
/// </summary>
public sealed partial class AiAssistantService(
    LexFlowDbContext db,
    ILlmProvider llmProvider,
    IAiPromptTemplateService templates,
    IRagRetrievalService ragRetrieval,
    IAiRetrievalGuard retrievalGuard,
    ICitationVerifier citationVerifier,
    IAiQuotaService quotaService,
    IAiInteractionAuditService auditService,
    IDocumentService documentService,
    IMatterService matterService,
    IBlobStorageService blobStorage,
    ITextExtractionService textExtraction) : IAiAssistantService
{
    private const string DocumentsContainer = "documents";

    private static readonly Dictionary<string, decimal> CreditCost = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chat"] = 1,
        ["document-summary"] = 2,
        ["matter-summary"] = 2,
        ["contract-review"] = 3,
        ["draft"] = 2,
        ["research"] = 2,
        ["similar-matters"] = 0.5m,
        ["hearing-prediction"] = 0.5m,
        ["risk-analysis"] = 1.5m,
        ["email-generator"] = 1,
        ["meeting-summary"] = 2,
    };

    [GeneratedRegex(@"\[REF:(?<kind>\w+):(?<id>[0-9a-fA-F-]{36})\]")]
    private static partial Regex CitationTokenPattern();

    public async Task<AiChatResponse> ChatAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, AiChatRequest request, CancellationToken cancellationToken = default)
    {
        await quotaService.EnsureWithinQuotaAsync(tenantId, CreditCost["chat"], cancellationToken);

        var template = templates.Get("chat");
        var chunks = await ragRetrieval.RetrieveAsync(tenantId, callerId, callerPermissions, request.Message, topK: 6, cancellationToken: cancellationToken);
        var history = request.History is null ? string.Empty : string.Join("\n", request.History.Select(h => $"{h.Role}: {h.Content}"));

        var userPrompt = template.Render(new Dictionary<string, string>
        {
            ["context"] = BuildContextText(chunks),
            ["history"] = history,
            ["message"] = request.Message,
        });

        var completion = await CallLlmAsync(template, userPrompt, cancellationToken);
        var citations = await ExtractVerifiedCitationsAsync(tenantId, callerId, callerPermissions, completion.Text, cancellationToken);
        var interactionId = await AuditAsync(tenantId, "chat", template, completion, callerId, null, null, request.Message, cancellationToken);

        return new AiChatResponse(interactionId, completion.Text, citations);
    }

    public async Task<AiDocumentSummaryResponse> SummarizeDocumentAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, Guid documentId, string? lengthPreset, CancellationToken cancellationToken = default)
    {
        await quotaService.EnsureWithinQuotaAsync(tenantId, CreditCost["document-summary"], cancellationToken);

        var content = await GetAccessibleDocumentTextAsync(tenantId, callerId, callerPermissions, documentId, cancellationToken);
        var template = templates.Get("document-summary");
        var userPrompt = template.Render(new Dictionary<string, string> { ["lengthPreset"] = lengthPreset ?? "standard", ["content"] = content });
        var completion = await CallLlmAsync(template, userPrompt, cancellationToken);
        var interactionId = await AuditAsync(tenantId, "document-summary", template, completion, callerId, "Document", documentId, null, cancellationToken);

        var keyPoints = completion.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.StartsWith('-') || l.StartsWith('•'))
            .Select(l => l.TrimStart('-', '•', ' '))
            .ToList();

        return new AiDocumentSummaryResponse(interactionId, completion.Text, keyPoints);
    }

    public async Task<AiMatterSummaryResponse> SummarizeMatterAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, Guid matterId, CancellationToken cancellationToken = default)
    {
        await quotaService.EnsureWithinQuotaAsync(tenantId, CreditCost["matter-summary"], cancellationToken);
        await EnsureMatterAccessAsync(tenantId, callerId, callerPermissions, matterId, cancellationToken);

        var context = await BuildMatterContextAsync(tenantId, matterId, cancellationToken);
        var template = templates.Get("matter-summary");
        var userPrompt = template.Render(new Dictionary<string, string> { ["context"] = context });
        var completion = await CallLlmAsync(template, userPrompt, cancellationToken);
        var interactionId = await AuditAsync(tenantId, "matter-summary", template, completion, callerId, "Matter", matterId, null, cancellationToken);

        var nextSteps = completion.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.StartsWith('-') || l.StartsWith('•'))
            .Select(l => l.TrimStart('-', '•', ' '))
            .ToList();

        return new AiMatterSummaryResponse(interactionId, completion.Text, nextSteps);
    }

    public async Task<AiContractReviewResponse> ReviewContractAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, Guid documentId, CancellationToken cancellationToken = default)
    {
        await quotaService.EnsureWithinQuotaAsync(tenantId, CreditCost["contract-review"], cancellationToken);

        var content = await GetAccessibleDocumentTextAsync(tenantId, callerId, callerPermissions, documentId, cancellationToken);
        var template = templates.Get("contract-review");
        var userPrompt = template.Render(new Dictionary<string, string> { ["content"] = content });
        var completion = await CallLlmAsync(template, userPrompt, cancellationToken);
        var interactionId = await AuditAsync(tenantId, "contract-review", template, completion, callerId, "Document", documentId, null, cancellationToken);

        var clauses = ParseContractClauses(completion.Text);
        var riskFlags = clauses.Where(c => string.Equals(c.RiskLevel, "high", StringComparison.OrdinalIgnoreCase)).Select(c => c.ClauseType).ToList();

        return new AiContractReviewResponse(interactionId, clauses, riskFlags);
    }

    public async Task<AiDraftResponse> DraftAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, AiDraftRequest request, CancellationToken cancellationToken = default)
    {
        await quotaService.EnsureWithinQuotaAsync(tenantId, CreditCost["draft"], cancellationToken);
        await EnsureMatterAccessAsync(tenantId, callerId, callerPermissions, request.MatterId, cancellationToken);

        var templateKey = string.Equals(request.Kind, "agreement", StringComparison.OrdinalIgnoreCase) ? "draft-agreement" : "draft-notice";
        var template = templates.Get(templateKey);
        var intake = string.Join("\n", request.IntakeFields.Select(kv => $"{kv.Key}: {kv.Value}"));
        var userPrompt = template.Render(new Dictionary<string, string> { ["template"] = "(firm default skeleton)", ["intake"] = intake });
        var completion = await CallLlmAsync(template, userPrompt, cancellationToken);
        var interactionId = await AuditAsync(tenantId, "draft", template, completion, callerId, "Matter", request.MatterId, null, cancellationToken);

        return new AiDraftResponse(interactionId, completion.Text);
    }

    public async Task<AiResearchResponse> ResearchAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, string question, bool webGroundedMode, CancellationToken cancellationToken = default)
    {
        await quotaService.EnsureWithinQuotaAsync(tenantId, CreditCost["research"], cancellationToken);

        var template = templates.Get("research");
        var chunks = await ragRetrieval.RetrieveAsync(tenantId, callerId, callerPermissions, question, topK: 8, sourceKinds: ["KbActSection", "KbJudgment", "KbArticle"], cancellationToken: cancellationToken);
        var userPrompt = template.Render(new Dictionary<string, string> { ["context"] = BuildContextText(chunks), ["question"] = question });
        var completion = await CallLlmAsync(template, userPrompt, cancellationToken);
        var citations = await ExtractVerifiedCitationsAsync(tenantId, callerId, callerPermissions, completion.Text, cancellationToken);
        var interactionId = await AuditAsync(tenantId, "research", template, completion, callerId, null, null, question, cancellationToken);

        var noAuthorityFound = completion.Text.Contains("no authority found", StringComparison.OrdinalIgnoreCase);
        return new AiResearchResponse(interactionId, completion.Text, citations, noAuthorityFound);
    }

    /// <summary>Feature #7: "surfaces similar past firm matters ... via embedding similarity" — pure RAG similarity, no LLM completion (matches the PRD's own description).</summary>
    public async Task<AiSimilarMattersResponse> GetSimilarMattersAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, Guid matterId, int topN, CancellationToken cancellationToken = default)
    {
        await quotaService.EnsureWithinQuotaAsync(tenantId, CreditCost["similar-matters"], cancellationToken);
        var matter = await EnsureMatterAccessAsync(tenantId, callerId, callerPermissions, matterId, cancellationToken);

        var queryText = $"{matter.Title} {matter.MatterType} {matter.Description}";
        var chunks = await ragRetrieval.RetrieveAsync(tenantId, callerId, callerPermissions, queryText, topK: topN + 5, sourceKinds: ["Matter"], cancellationToken: cancellationToken);

        var matches = new List<AiSimilarMatter>();
        foreach (var chunk in chunks.Where(c => c.SourceId != matterId))
        {
            if (matches.Count >= topN)
            {
                break;
            }

            var candidate = await matterService.GetByIdAsync(tenantId, chunk.SourceId, cancellationToken);
            if (candidate is not null)
            {
                matches.Add(new AiSimilarMatter(candidate.Id, candidate.Title, chunk.Score));
            }
        }

        var interactionId = await auditService.RecordAsync(new AiInteractionRecord(tenantId, "similar-matters", "embedding-similarity", "1.0.0", "hashing-embedding", 0, 0, 0, CreditCost["similar-matters"], callerId, "Matter", matterId, matter.Title, null), cancellationToken);
        return new AiSimilarMattersResponse(interactionId, matches);
    }

    /// <summary>Feature #8: heuristic stand-in for the PRD's "simple gradient-boosted model" — see AiHearingPredictionResponse's own doc comment. Edge Case: "prediction with &lt;50 historical samples -&gt; feature hidden 'insufficient data'."</summary>
    public async Task<AiHearingPredictionResponse> PredictNextHearingAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, Guid caseId, CancellationToken cancellationToken = default)
    {
        await quotaService.EnsureWithinQuotaAsync(tenantId, CreditCost["hearing-prediction"], cancellationToken);

        var targetCase = await db.CourtCases.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == caseId, cancellationToken)
            ?? throw new Application.Common.Exceptions.NotFoundException("CourtCase", caseId);
        await EnsureMatterAccessAsync(tenantId, callerId, callerPermissions, targetCase.MatterId, cancellationToken);

        var peerCaseIds = await db.CourtCases
            .Where(c => c.TenantId == tenantId && c.CourtId == targetCase.CourtId && c.CaseType == targetCase.CaseType && c.Stage == targetCase.Stage)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var peerHearings = await db.Hearings
            .Where(h => h.TenantId == tenantId && peerCaseIds.Contains(h.CaseId))
            .OrderBy(h => h.CaseId).ThenBy(h => h.Date)
            .ToListAsync(cancellationToken);

        const int minimumSamples = 50;
        if (peerHearings.Count < minimumSamples)
        {
            var fallbackInteractionId = await auditService.RecordAsync(new AiInteractionRecord(tenantId, "hearing-prediction", "statistical-heuristic", "1.0.0", "statistical-heuristic", 0, 0, 0, CreditCost["hearing-prediction"], callerId, "CourtCase", caseId, null, "insufficient-data"), cancellationToken);
            return new AiHearingPredictionResponse(fallbackInteractionId, 0, 0, 0, "Insufficient", true);
        }

        var adjournedCount = peerHearings.Count(h => h.Status == "Adjourned");
        var adjournmentProbability = Math.Round((double)adjournedCount / peerHearings.Count, 2);

        var gapDays = peerHearings
            .GroupBy(h => h.CaseId)
            .SelectMany(g => g.Zip(g.Skip(1), (a, b) => (b.Date.ToDateTime(TimeOnly.MinValue) - a.Date.ToDateTime(TimeOnly.MinValue)).Days))
            .Where(d => d > 0)
            .ToList();

        var (low, high) = gapDays.Count > 0
            ? ((int)gapDays.Average() - (int)StdDev(gapDays), (int)gapDays.Average() + (int)StdDev(gapDays))
            : (0, 0);

        var interactionId = await auditService.RecordAsync(new AiInteractionRecord(tenantId, "hearing-prediction", "statistical-heuristic", "1.0.0", "statistical-heuristic", 0, 0, 0, CreditCost["hearing-prediction"], callerId, "CourtCase", caseId, null, $"p={adjournmentProbability}"), cancellationToken);
        return new AiHearingPredictionResponse(interactionId, adjournmentProbability, Math.Max(0, low), Math.Max(low, high), "Advisory", false);
    }

    /// <summary>Feature #9: deterministic rule-based factor scoring (limitation proximity, payment default, stage stagnation) with an LLM-written rationale paragraph. Adverse-orders-language NLP is not implemented (documented gap — would need order-text sentiment analysis this build doesn't have).</summary>
    public async Task<AiRiskAnalysisResponse> GetRiskAnalysisAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, Guid matterId, CancellationToken cancellationToken = default)
    {
        await quotaService.EnsureWithinQuotaAsync(tenantId, CreditCost["risk-analysis"], cancellationToken);
        var matter = await EnsureMatterAccessAsync(tenantId, callerId, callerPermissions, matterId, cancellationToken);

        var factors = new List<AiRiskFactor>();

        var nextDueDate = await db.MatterImportantDates
            .Where(d => d.TenantId == tenantId && d.MatterId == matterId && d.SatisfiedAt == null)
            .OrderBy(d => d.DueAt)
            .Select(d => (DateTimeOffset?)d.DueAt)
            .FirstOrDefaultAsync(cancellationToken);
        var daysToLimitation = nextDueDate.HasValue ? (int)(nextDueDate.Value - DateTimeOffset.UtcNow).TotalDays : (int?)null;
        var limitationScore = daysToLimitation switch { null => 0, <= 0 => 100, <= 30 => 80, <= 90 => 40, _ => 10 };
        factors.Add(new AiRiskFactor("Limitation proximity", limitationScore, daysToLimitation.HasValue ? $"{daysToLimitation} days to next unsatisfied important date." : "No pending important dates."));

        var overdueAmount = await db.Invoices
            .Where(i => i.TenantId == tenantId && i.MatterId == matterId && i.Status == "Overdue")
            .SumAsync(i => (decimal?)(i.GrandTotal - i.AmountPaid), cancellationToken) ?? 0;
        var paymentScore = overdueAmount switch { 0 => 0, > 0 and <= 50000 => 30, _ => 70 };
        factors.Add(new AiRiskFactor("Client payment default", paymentScore, $"{overdueAmount:0.00} overdue across unpaid invoices."));

        var caseIds = await db.CourtCases.Where(c => c.TenantId == tenantId && c.MatterId == matterId).Select(c => c.Id).ToListAsync(cancellationToken);
        var lastStageChange = await db.CaseStageHistory.Where(h => h.TenantId == tenantId && caseIds.Contains(h.CaseId)).OrderByDescending(h => h.At).Select(h => (DateTimeOffset?)h.At).FirstOrDefaultAsync(cancellationToken);
        var stagnationDays = lastStageChange.HasValue ? (int)(DateTimeOffset.UtcNow - lastStageChange.Value).TotalDays : (int)(DateTimeOffset.UtcNow - matter.CreatedAt).TotalDays;
        var stagnationScore = stagnationDays switch { <= 60 => 0, <= 90 => 30, <= 180 => 60, _ => 90 };
        factors.Add(new AiRiskFactor("Stage stagnation", stagnationScore, $"{stagnationDays} days since last recorded stage change."));

        var riskScore = (int)Math.Round(factors.Average(f => f.Score));

        var template = templates.Get("risk-analysis-rationale");
        var userPrompt = template.Render(new Dictionary<string, string> { ["factors"] = string.Join("\n", factors.Select(f => $"{f.Name}: {f.Score}/100 — {f.Detail}")) });
        var completion = await CallLlmAsync(template, userPrompt, cancellationToken);
        var interactionId = await AuditAsync(tenantId, "risk-analysis", template, completion, callerId, "Matter", matterId, null, cancellationToken);

        return new AiRiskAnalysisResponse(interactionId, riskScore, factors, completion.Text);
    }

    public async Task<AiEmailGenerateResponse> GenerateEmailAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, AiEmailGenerateRequest request, CancellationToken cancellationToken = default)
    {
        await quotaService.EnsureWithinQuotaAsync(tenantId, CreditCost["email-generator"], cancellationToken);

        var template = templates.Get("email-generator");
        var userPrompt = template.Render(new Dictionary<string, string>
        {
            ["intent"] = request.Intent,
            ["tone"] = request.Tone,
            ["bulletPoints"] = string.Join("\n", request.BulletPoints.Select(b => $"- {b}")),
            ["threadContext"] = request.ThreadContext ?? "(new email, no thread)",
        });
        var completion = await CallLlmAsync(template, userPrompt, cancellationToken);
        var interactionId = await AuditAsync(tenantId, "email-generator", template, completion, callerId, null, null, request.Intent, cancellationToken);

        var lines = completion.Text.Split('\n', 2, StringSplitOptions.None);
        var subject = lines.Length > 0 ? lines[0].Replace("Subject:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim() : request.Intent;
        var body = lines.Length > 1 ? lines[1].Trim() : completion.Text;

        return new AiEmailGenerateResponse(interactionId, subject, body);
    }

    public async Task<AiMeetingSummaryResponse> SummarizeMeetingAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, Guid transcriptionId, CancellationToken cancellationToken = default)
    {
        await quotaService.EnsureWithinQuotaAsync(tenantId, CreditCost["meeting-summary"], cancellationToken);

        var transcription = await db.AiTranscriptions.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == transcriptionId, cancellationToken)
            ?? throw new Application.Common.Exceptions.NotFoundException("AiTranscription", transcriptionId);

        if (transcription.MatterId.HasValue)
        {
            await EnsureMatterAccessAsync(tenantId, callerId, callerPermissions, transcription.MatterId.Value, cancellationToken);
        }
        else if (transcription.RequestedBy != callerId)
        {
            throw new Application.Common.Exceptions.ForbiddenAccessException();
        }

        if (transcription.Status != "Done" || string.IsNullOrWhiteSpace(transcription.TranscriptText))
        {
            throw new Application.Common.Exceptions.DomainRuleException("TRANSCRIPT_NOT_READY", $"Transcription {transcriptionId} is not ready yet (status {transcription.Status}).");
        }

        var template = templates.Get("meeting-summary");
        var userPrompt = template.Render(new Dictionary<string, string> { ["transcript"] = transcription.TranscriptText });
        var completion = await CallLlmAsync(template, userPrompt, cancellationToken);
        var interactionId = await AuditAsync(tenantId, "meeting-summary", template, completion, callerId, "AiTranscription", transcriptionId, null, cancellationToken);

        var decisions = ExtractBulletSection(completion.Text, "Decisions");
        var actionItems = ExtractBulletSection(completion.Text, "Action Items");

        return new AiMeetingSummaryResponse(interactionId, completion.Text, decisions, actionItems);
    }

    private static double StdDev(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var mean = values.Average();
        return Math.Sqrt(values.Sum(v => Math.Pow(v - mean, 2)) / values.Count);
    }

    private static List<string> ExtractBulletSection(string text, string sectionHeading)
    {
        var lines = text.Split('\n');
        var inSection = false;
        var items = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Contains(sectionHeading, StringComparison.OrdinalIgnoreCase))
            {
                inSection = true;
                continue;
            }

            if (inSection)
            {
                if (line.StartsWith('-') || line.StartsWith('•'))
                {
                    items.Add(line.TrimStart('-', '•', ' '));
                }
                else if (line.Length > 0 && !line.StartsWith('-') && !line.StartsWith('•') && items.Count > 0)
                {
                    break;
                }
            }
        }

        return items;
    }

    private static string BuildContextText(IReadOnlyList<RagChunk> chunks) =>
        chunks.Count == 0
            ? "(no accessible records found)"
            : string.Join("\n\n", chunks.Select(c => $"[REF:{c.SourceKind}:{c.SourceId}]\n{c.ChunkText}"));

    private async Task<IReadOnlyList<AiCitation>> ExtractVerifiedCitationsAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, string text, CancellationToken cancellationToken)
    {
        var matches = CitationTokenPattern().Matches(text);
        if (matches.Count == 0)
        {
            return [];
        }

        var citations = matches
            .Select(m => Guid.TryParse(m.Groups["id"].Value, out var id) ? new AiCitation(m.Groups["kind"].Value, id, null) : null)
            .Where(c => c is not null)
            .Select(c => c!)
            .DistinctBy(c => (c.Kind, c.Id))
            .ToList();

        var result = await citationVerifier.VerifyAsync(tenantId, callerId, callerPermissions, citations, cancellationToken);
        return result.Verified;
    }

    private async Task<(LlmCompletionResult Completion, int LatencyMs)> CallLlmWithLatencyAsync(AiPromptTemplate template, string userPrompt, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await llmProvider.CompleteAsync(
            new LlmCompletionRequest(template.Model, template.SystemPrompt, [new LlmMessage("user", userPrompt)], template.MaxTokens, template.Temperature),
            cancellationToken);
        stopwatch.Stop();
        return (result, (int)stopwatch.ElapsedMilliseconds);
    }

    private async Task<LlmCompletionResult> CallLlmAsync(AiPromptTemplate template, string userPrompt, CancellationToken cancellationToken)
    {
        var (completion, latencyMs) = await CallLlmWithLatencyAsync(template, userPrompt, cancellationToken);
        _pendingLatencyMs = latencyMs;
        return completion;
    }

    private int _pendingLatencyMs;

    private async Task<Guid> AuditAsync(Guid tenantId, string feature, AiPromptTemplate template, LlmCompletionResult completion, Guid callerId, string? targetRefKind, Guid? targetRefId, string? inputText, CancellationToken cancellationToken) =>
        await auditService.RecordAsync(
            new AiInteractionRecord(tenantId, feature, template.Key, template.Version, completion.Model, completion.TokensInput, completion.TokensOutput, _pendingLatencyMs, CreditCost.GetValueOrDefault(feature, 1), callerId, targetRefKind, targetRefId, inputText, completion.Text),
            cancellationToken);

    private async Task<string> GetAccessibleDocumentTextAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, Guid documentId, CancellationToken cancellationToken)
    {
        if (!await retrievalGuard.CanAccessAsync(tenantId, callerId, callerPermissions, "Document", documentId, cancellationToken))
        {
            throw new Application.Common.Exceptions.NotFoundException("Document", documentId);
        }

        var document = await documentService.GetByIdAsync(tenantId, documentId, callerPermissions, cancellationToken)
            ?? throw new Application.Common.Exceptions.NotFoundException("Document", documentId);

        if (document.CurrentVersionId is null)
        {
            return string.Empty;
        }

        var version = await db.DocumentVersions.SingleAsync(v => v.Id == document.CurrentVersionId, cancellationToken);
        var content = await blobStorage.DownloadAsync(DocumentsContainer, version.BlobPath, cancellationToken);
        var extraction = await textExtraction.ExtractAsync(content, version.Mime, cancellationToken);
        return extraction.Text ?? string.Empty;
    }

    private async Task<Application.Common.Interfaces.MatterDto> EnsureMatterAccessAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, Guid matterId, CancellationToken cancellationToken)
    {
        if (!await retrievalGuard.CanAccessAsync(tenantId, callerId, callerPermissions, "Matter", matterId, cancellationToken))
        {
            throw new Application.Common.Exceptions.NotFoundException("Matter", matterId);
        }

        return await matterService.GetByIdAsync(tenantId, matterId, cancellationToken)
            ?? throw new Application.Common.Exceptions.NotFoundException("Matter", matterId);
    }

    private async Task<string> BuildMatterContextAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken)
    {
        var matter = await matterService.GetByIdAsync(tenantId, matterId, cancellationToken);
        var cases = await db.CourtCases.Where(c => c.TenantId == tenantId && c.MatterId == matterId).ToListAsync(cancellationToken);
        var caseIds = cases.Select(c => c.Id).ToList();
        var hearings = await db.Hearings.Where(h => h.TenantId == tenantId && caseIds.Contains(h.CaseId)).OrderBy(h => h.Date).ToListAsync(cancellationToken);
        var orders = await db.CourtOrders.Where(o => o.TenantId == tenantId && caseIds.Contains(o.CaseId)).ToListAsync(cancellationToken);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[REF:Matter:{matterId}]");
        sb.AppendLine($"Matter: {matter?.Title} ({matter?.Status}), opened {matter?.OpenedOn}, type {matter?.MatterType}.");
        foreach (var c in cases)
        {
            sb.AppendLine($"Case {c.CaseNumber} ({c.CaseType}, {c.CaseYear}) — stage: {c.Stage}, status: {c.Status}.");
        }

        foreach (var h in hearings)
        {
            sb.AppendLine($"Hearing {h.Date}: {h.Purpose} — status {h.Status}.");
        }

        foreach (var o in orders)
        {
            sb.AppendLine($"Order {o.OrderDate}: {o.Gist}");
        }

        return sb.ToString();
    }

    private static List<AiContractClause> ParseContractClauses(string text)
    {
        var clauses = new List<AiContractClause>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var clauseType = line[..separatorIndex].TrimStart('-', '•', ' ');
            var rest = line[(separatorIndex + 1)..].Trim();
            var riskLevel = rest.Contains("high risk", StringComparison.OrdinalIgnoreCase) ? "high"
                : rest.Contains("medium risk", StringComparison.OrdinalIgnoreCase) ? "medium"
                : rest.Contains("low risk", StringComparison.OrdinalIgnoreCase) ? "low"
                : null;

            clauses.Add(new AiContractClause(clauseType, rest, riskLevel));
        }

        return clauses;
    }
}
