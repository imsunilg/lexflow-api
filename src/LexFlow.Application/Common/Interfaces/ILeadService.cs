namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 2 (Lead Management) lifecycle operations — PRD §17 API list.</summary>
public interface ILeadService
{
    Task<LeadDto> CreateAsync(Guid tenantId, Guid? actorId, CreateLeadInput input, CancellationToken cancellationToken = default);

    Task<LeadDto?> GetByIdAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeadDto>> GetAllAsync(Guid tenantId, LeadFilter filter, CancellationToken cancellationToken = default);

    Task<LeadDto> UpdateAsync(Guid tenantId, Guid leadId, UpdateLeadInput input, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// AC-L2: writes lead_stage_history and returns within the SLA the UI polls against.
    /// Non-adjacent transitions in the default pipeline order require <paramref name="allowSkip"/>
    /// (Module 2 Validation Rules: "skip allowed if lead.stage.skip permission").
    /// </summary>
    Task<LeadDto> ChangeStageAsync(Guid tenantId, Guid? actorId, Guid leadId, string toStage, string? note, bool allowSkip, CancellationToken cancellationToken = default);

    Task<LeadActivityDto> AddActivityAsync(Guid tenantId, Guid? actorId, Guid leadId, string activityType, string? direction, int? durationMin, string? subject, string? body, string? outcome, CancellationToken cancellationToken = default);

    /// <summary>Direct userId assignment, or a simple round-robin rule when only a ruleId is given (Module 2: "auto-assignment engine").</summary>
    Task<LeadDto> AssignAsync(Guid tenantId, Guid leadId, Guid? userId, Guid? ruleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// AC-L3: atomic Client (+ optional Matter + optional Invoice) creation. Matter/Invoice
    /// creation requires Legal (04_Legal)/Fin (06_Fin) modules that do not exist yet in this
    /// build (they ship in later prompts per the Build Playbook's own ordering) — requesting
    /// createMatter or an invoicePayload throws <see cref="Exceptions.ConflictException"/>
    /// with code MODULE_NOT_AVAILABLE rather than silently no-op'ing or partially committing.
    /// </summary>
    Task<ConvertLeadResult> ConvertAsync(Guid tenantId, Guid? actorId, Guid leadId, bool createMatter, string? matterPayloadJson, string? invoicePayloadJson, bool force, CancellationToken cancellationToken = default);

    Task<LeadDto> MarkLostAsync(Guid tenantId, Guid? actorId, Guid leadId, Guid lostReasonId, string? note, CancellationToken cancellationToken = default);

    /// <summary>Phone/email exact + pg_trgm fuzzy name match (Module 2 User Flow step 2).</summary>
    Task<IReadOnlyList<DuplicateMatchDto>> CheckDuplicatesAsync(Guid tenantId, string? name, string? email, string? phone, CancellationToken cancellationToken = default);

    Task<byte[]> ExportAsync(Guid tenantId, LeadFilter filter, string format, CancellationToken cancellationToken = default);

    /// <summary>Unauthenticated capture path (Module 2: "web-to-lead API/embed form"). tenantKey resolves the tenant from the embed form's public key.</summary>
    Task<LeadDto> CaptureFromWebToLeadAsync(Guid tenantId, WebToLeadInput input, CancellationToken cancellationToken = default);
}

public sealed record CreateLeadInput(
    string FirstName,
    string? LastName,
    string? Company,
    string? Email,
    string? PhoneE164,
    Guid? SourceId,
    Guid? OwnerId,
    Guid? BranchId,
    Guid? PracticeAreaId,
    string? IssueSummary,
    string? OpposingParty,
    string? BudgetBand);

public sealed record UpdateLeadInput(
    string FirstName,
    string? LastName,
    string? Company,
    string? Email,
    string? PhoneE164,
    Guid? SourceId,
    Guid? PracticeAreaId,
    string? IssueSummary,
    string? OpposingParty,
    string? BudgetBand);

public sealed record WebToLeadInput(
    string FirstName,
    string? LastName,
    string? Email,
    string? PhoneE164,
    string? IssueSummary,
    string? Honeypot);

public sealed record LeadFilter(string? Stage, Guid? SourceId, Guid? OwnerId, int? MinScore, DateTimeOffset? CreatedFrom, DateTimeOffset? CreatedTo, string? Query, string? Status);

public sealed record ConvertLeadResult(Guid ClientId, Guid? MatterId, Guid? InvoiceId);

public sealed record DuplicateMatchDto(Guid LeadId, string DisplayName, string? Email, string? PhoneE164, double Similarity, string MatchKind);

public sealed record LeadActivityDto(Guid Id, Guid LeadId, string ActivityType, string? Direction, int? DurationMin, string? Subject, string? Body, string? Outcome, DateTimeOffset OccurredAt, Guid? LoggedBy);

public sealed record LeadDto(
    Guid Id,
    string Number,
    string FirstName,
    string? LastName,
    string? Company,
    string? Email,
    string? PhoneE164,
    Guid? SourceId,
    string Stage,
    Guid? OwnerId,
    Guid? BranchId,
    Guid? PracticeAreaId,
    int Score,
    string? IssueSummary,
    string? OpposingParty,
    string? BudgetBand,
    string Status,
    Guid? LostReasonId,
    Guid? ConvertedClientId,
    DateTimeOffset? SlaFirstContactDue,
    DateTimeOffset? FirstContactedAt,
    DateTimeOffset CreatedAt);
