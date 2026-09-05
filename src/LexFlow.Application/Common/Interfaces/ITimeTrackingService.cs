namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 9: timers (server-anchored start/pause/resume/stop) + the manual-entry/timesheet/
/// approval workflow. BR-5 (Billed entries immutable) is enforced first at the DB (fin.time_entries
/// 004_Triggers.sql) and again defensively in TimeEntry's own mutators; any DbUpdateException whose
/// inner message carries the trigger's BR-5 text is translated here to DomainRuleException("
/// TIME_ENTRY_BILLED", ...) so callers never see a raw Postgres error (defense in depth).
/// </summary>
public interface ITimeTrackingService
{
    Task<RunningTimerDto> StartTimerAsync(Guid tenantId, Guid userId, StartTimerInput input, CancellationToken cancellationToken = default);

    Task<RunningTimerDto> PauseTimerAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<RunningTimerDto> ResumeTimerAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>AC-T1: converts the running timer into a Draft fin.time_entries row, rounded per firm rule, then deletes the timer row.</summary>
    Task<TimeEntryDto> StopTimerAsync(Guid tenantId, Guid userId, StopTimerInput input, CancellationToken cancellationToken = default);

    Task<RunningTimerDto?> GetCurrentTimerAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<TimeEntryDto> CreateManualEntryAsync(Guid tenantId, Guid userId, CreateTimeEntryInput input, CancellationToken cancellationToken = default);

    Task<TimeEntryDto> UpdateEntryAsync(Guid tenantId, Guid entryId, UpdateTimeEntryInput input, CancellationToken cancellationToken = default);

    Task DeleteEntryAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken = default);

    Task<TimeEntryDto?> GetEntryAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> GetEntriesAsync(Guid tenantId, TimeEntryFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> SubmitAsync(Guid tenantId, IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Module 9 Security Rules: "time.approve cannot approve own entries (segregation)" — throws DomainRuleException("SELF_APPROVAL_NOT_ALLOWED", ...) if approverId == entry.UserId for any id.</summary>
    Task<IReadOnlyList<TimeEntryDto>> ApproveAsync(Guid tenantId, Guid approverId, IReadOnlyList<Guid> ids, decimal? manualRateOverride, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> RejectAsync(Guid tenantId, Guid approverId, IReadOnlyList<Guid> ids, string? comment, CancellationToken cancellationToken = default);
}

public sealed record StartTimerInput(Guid? MatterId, Guid? ActivityCodeId, string? ContextRef);

public sealed record StopTimerInput(bool Billable, string? Narrative, string? InternalNote, Guid? ActivityCodeId, Guid? MatterId);

public sealed record CreateTimeEntryInput(Guid MatterId, Guid? ActivityCodeId, DateOnly EntryDate, DateTimeOffset? StartedAt, int DurationMin, bool Billable, string? Narrative, string? InternalNote);

public sealed record UpdateTimeEntryInput(Guid MatterId, Guid? ActivityCodeId, DateOnly EntryDate, int DurationMin, bool Billable, string? Narrative, string? InternalNote);

public sealed record TimeEntryFilter(Guid? UserId, Guid? MatterId, string? Status, DateOnly? From, DateOnly? To);

public sealed record RunningTimerDto(Guid UserId, Guid? MatterId, DateTimeOffset StartedAt, bool IsPaused, TimeSpan Elapsed, string ContextJson);

public sealed record TimeEntryDto(
    Guid Id, Guid UserId, Guid MatterId, Guid? ActivityCodeId, DateOnly EntryDate, DateTimeOffset? StartedAt,
    int DurationMin, int RoundedMin, bool Billable, string? Narrative, string? InternalNote, string Status,
    decimal? RateSnapshot, decimal? AmountSnapshot, Guid? InvoiceLineId, string Source, Guid? ApprovedBy, DateTimeOffset? ApprovedAt);
