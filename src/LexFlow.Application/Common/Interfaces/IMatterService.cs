namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 4 (Matter Management) — PRD §17 API list.</summary>
public interface IMatterService
{
    Task<MatterDto> CreateAsync(Guid tenantId, Guid? actorId, CreateMatterInput input, CancellationToken cancellationToken = default);

    Task<MatterDto?> GetByIdAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MatterDto>> GetAllAsync(Guid tenantId, MatterFilter filter, CancellationToken cancellationToken = default);

    Task<MatterDto> UpdateAsync(Guid tenantId, Guid matterId, UpdateMatterInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// AC-M3: closing enforces the checklist (no running timers, unbilled-time decision,
    /// trust-balance disposition) before allowing Closed; reopen requires a reason.
    /// </summary>
    Task<MatterDto> ChangeStatusAsync(Guid tenantId, Guid? actorId, Guid matterId, string toStatus, string? outcome, string? closureNote, bool allowReopen, CancellationToken cancellationToken = default);

    Task AddTeamMemberAsync(Guid tenantId, Guid matterId, Guid userId, string? roleInMatter, decimal? rateOverride, CancellationToken cancellationToken = default);

    Task RemoveTeamMemberAsync(Guid tenantId, Guid matterId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MatterTeamMemberDto>> GetTeamAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);

    Task<MatterPartyDto> AddPartyAsync(Guid tenantId, Guid matterId, MatterPartyInput input, CancellationToken cancellationToken = default);

    Task<MatterPartyDto> UpdatePartyAsync(Guid tenantId, Guid matterId, Guid partyId, MatterPartyInput input, CancellationToken cancellationToken = default);

    Task DeletePartyAsync(Guid tenantId, Guid matterId, Guid partyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MatterPartyDto>> GetPartiesAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);

    /// <summary>BR-2: limitation date must be &gt;= open date; raises a warning (not a hard block) if &lt; 30 days away at creation.</summary>
    Task<MatterImportantDateDto> AddImportantDateAsync(Guid tenantId, Guid matterId, ImportantDateInput input, CancellationToken cancellationToken = default);

    Task<MatterImportantDateDto> UpdateImportantDateAsync(Guid tenantId, Guid matterId, Guid dateId, ImportantDateInput input, CancellationToken cancellationToken = default);

    /// <summary>BR-2: dates within 30 days of due cannot be deleted (DB trigger backstop) — mark satisfied instead.</summary>
    Task DeleteImportantDateAsync(Guid tenantId, Guid matterId, Guid dateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MatterImportantDateDto>> GetImportantDatesAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);

    Task<MatterExpenseDto> AddExpenseAsync(Guid tenantId, Guid matterId, MatterExpenseInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MatterExpenseDto>> GetExpensesAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);

    Task<MatterRelatedDto> AddRelatedAsync(Guid tenantId, Guid matterId, Guid relatedMatterId, string relationType, CancellationToken cancellationToken = default);

    /// <summary>AC-M2: merges hearings/orders/tasks/notes/documents/time-entries chronologically, cursor-paged. Only the entity types actually modeled in this build (hearings, orders, important-date satisfactions, status changes) are populated; the rest render empty until their modules exist.</summary>
    Task<TimelinePage> GetTimelineAsync(Guid tenantId, Guid matterId, string? types, string? cursor, CancellationToken cancellationToken = default);

    /// <summary>AC-M4: sums fin.invoices/fin.payments/fin.time_entries against this matter (dynamically probed via to_regclass so it degrades gracefully if those schemas are ever unavailable).</summary>
    Task<MatterFinancialSummaryDto> GetFinancialSummaryAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);
}

public sealed record CreateMatterInput(
    string Title,
    Guid ClientId,
    string MatterType,
    Guid? PracticeAreaId,
    Guid? BranchId,
    Guid? ResponsibleLawyerId,
    string Priority,
    string? Description,
    DateOnly OpenedOn,
    bool IsPrivate,
    decimal? Budget,
    string? BillingArrangementJson,
    IReadOnlyList<string>? OppositePartyNames,
    bool OverrideConflict,
    string? ConflictOverrideReason);

public sealed record UpdateMatterInput(
    string Title,
    string MatterType,
    Guid? PracticeAreaId,
    Guid? BranchId,
    Guid? ResponsibleLawyerId,
    string Priority,
    string? Description,
    decimal? Budget,
    bool IsPrivate,
    string? BillingArrangementJson);

public sealed record MatterFilter(string? Status, string? MatterType, Guid? PracticeAreaId, Guid? ResponsibleLawyerId, string? Priority, string? Query);

public sealed record MatterTeamMemberDto(Guid MatterId, Guid UserId, string? RoleInMatter, decimal? RateOverride);

public sealed record MatterPartyInput(string Name, string PartyRole, string? AdvocateName, string ContactJson);

public sealed record MatterPartyDto(Guid Id, Guid MatterId, string Name, string PartyRole, string? AdvocateName, string ContactJson);

public sealed record ImportantDateInput(string Kind, string Title, DateTimeOffset DueAt, string? ReminderPolicyJson, string? Severity);

public sealed record MatterImportantDateDto(Guid Id, Guid MatterId, string Kind, string Title, DateTimeOffset DueAt, DateTimeOffset? SatisfiedAt, string? SatisfiedNote, string? Severity);

public sealed record MatterExpenseInput(DateOnly IncurredOn, string? Category, string? Description, decimal Amount, bool Billable, Guid? ReceiptDocumentId);

public sealed record MatterExpenseDto(Guid Id, Guid MatterId, DateOnly IncurredOn, string? Category, string? Description, decimal Amount, bool Billable);

public sealed record MatterRelatedDto(Guid Id, Guid MatterId, Guid RelatedMatterId, string RelationType);

public sealed record TimelineEntry(string EntityType, Guid EntityId, DateTimeOffset At, string Summary);

public sealed record TimelinePage(IReadOnlyList<TimelineEntry> Entries, string? NextCursor);

public sealed record MatterFinancialSummaryDto(decimal Billed, decimal Collected, decimal Wip, decimal Expenses, decimal? BudgetVariance, decimal TrustBalance);

public sealed record MatterDto(
    Guid Id,
    string Number,
    string Title,
    Guid ClientId,
    string MatterType,
    Guid? PracticeAreaId,
    Guid? BranchId,
    Guid? ResponsibleLawyerId,
    string Priority,
    string Status,
    string? Outcome,
    DateOnly OpenedOn,
    DateOnly? ClosedOn,
    bool IsPrivate,
    decimal? Budget,
    string? Description,
    string BillingArrangementJson,
    DateTimeOffset CreatedAt);
