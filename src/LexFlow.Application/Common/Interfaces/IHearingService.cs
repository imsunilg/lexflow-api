namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 5 hearing lifecycle — PRD §17. <see cref="RecordOutcomeAsync"/> is the
/// G-AC2/AC-CC1 critical path: "Recording an outcome with next date creates next
/// hearing + reminders (30/7/1-day + same-day 7 AM) in one transaction." Every code
/// path that creates a hearing (this API, the future CSV import, mobile offline-sync)
/// must funnel through this same service method so the invariant can never be
/// bypassed — see LexFlow.Infrastructure.Legal.HearingService's single insertion point.
/// </summary>
public interface IHearingService
{
    Task<HearingDto> CreateAsync(Guid tenantId, Guid caseId, CreateHearingInput input, CancellationToken cancellationToken = default);

    Task<HearingDto?> GetByIdAsync(Guid tenantId, Guid hearingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HearingDto>> GetByCaseAsync(Guid tenantId, Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>Cause list: GET /hearings?date=&amp;courtId=&amp;lawyerId=. AC-CC2: renders &lt; 1s for 200 hearings.</summary>
    Task<IReadOnlyList<HearingDto>> GetCauseListAsync(Guid tenantId, DateOnly date, Guid? courtId, Guid? lawyerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// AC-CC1: records the outcome, and in the same transaction either creates the next
    /// hearing (+ 30/7/1-day + same-day-7AM reminders) or marks the case SineDie/Disposed —
    /// the BR-6/AC-CC3 DB constraint trigger (deferred to COMMIT) is the hard backstop that
    /// makes "one transaction" a guarantee, not just an implementation detail.
    /// </summary>
    Task<RecordOutcomeResult> RecordOutcomeAsync(Guid tenantId, Guid? actorId, Guid hearingId, RecordOutcomeInput input, CancellationToken cancellationToken = default);
}

public sealed record CreateHearingInput(DateOnly Date, TimeOnly? Time, string? Purpose, string? Courtroom, Guid? AssignedLawyerId, bool AllowBackdate);

public sealed record RecordOutcomeInput(
    string Summary,
    string? AdjournReason,
    DateOnly? NextHearingDate,
    TimeOnly? NextHearingTime,
    string? NextHearingPurpose,
    bool SineDie,
    bool Disposed,
    IReadOnlyList<CreateOrderInput>? Orders,
    bool CreateComplianceTask);

public sealed record CreateOrderInput(DateOnly OrderDate, string? Gist, DateOnly? ComplianceDue, Guid? DocumentId);

public sealed record RecordOutcomeResult(HearingOutcomeDto Outcome, HearingDto? NextHearing, IReadOnlyList<Guid> CreatedOrderIds, IReadOnlyList<Guid> CreatedReminderIds, Guid? ComplianceTaskId);

public sealed record HearingOutcomeDto(Guid Id, Guid HearingId, string Summary, string? AdjournReason, Guid? RecordedBy, DateTimeOffset RecordedAt);

public sealed record HearingDto(Guid Id, Guid CaseId, DateOnly Date, TimeOnly? Time, string CourtTz, string? Purpose, string? Courtroom, Guid? AssignedLawyerId, string Status);
