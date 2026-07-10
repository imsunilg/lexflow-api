namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 6 (Calendar) — native events plus RRULE occurrence expansion (24-month rolling
/// materialization) and the ops.v_calendar_items union (hearings/deadlines/tasks,
/// read-only/locked from this side per Module 6 validation rule).
/// </summary>
public interface ICalendarService
{
    Task<CalendarEventDto> CreateEventAsync(Guid tenantId, Guid? actorId, CreateCalendarEventInput input, CancellationToken cancellationToken = default);

    Task<CalendarEventDto?> GetByIdAsync(Guid tenantId, Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>scope=series updates every future occurrence (the master row); scope=occurrence writes a RecurrenceException instead (AC-CAL3).</summary>
    Task<CalendarEventDto> UpdateAsync(Guid tenantId, Guid eventId, UpdateCalendarEventInput input, string scope, DateOnly? occurrenceDate, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, Guid eventId, string scope, DateOnly? occurrenceDate, CancellationToken cancellationToken = default);

    /// <summary>Merges native calendar_events (RRULE-expanded via Ical.Net, exceptions applied) with ops.v_calendar_items (hearings/deadlines/tasks) over [from, to].</summary>
    Task<IReadOnlyList<CalendarItemDto>> GetCalendarAsync(Guid tenantId, DateTimeOffset from, DateTimeOffset to, IReadOnlyCollection<string>? types, CancellationToken cancellationToken = default);

    Task<EventReminderDto> AddReminderAsync(Guid tenantId, Guid eventId, int offsetMinutes, string channel, CancellationToken cancellationToken = default);

    Task<FreeBusyResult> GetFreeBusyAsync(Guid tenantId, IReadOnlyList<Guid> userIds, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    Task<string> GetOrCreateIcsTokenAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task RevokeIcsTokenAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Public/unauthenticated path (secret embeds tenant id) — returns an RFC 5545 VCALENDAR document for the token's owner.</summary>
    Task<string?> GetIcsFeedAsync(string token, CancellationToken cancellationToken = default);
}

public sealed record CreateCalendarEventInput(string Kind, string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool AllDay, string? Location, string? VideoLink, Guid? MatterId, string? Rrule, Guid? OrganizerId, IReadOnlyList<AttendeeInput>? Attendees);

public sealed record AttendeeInput(Guid? UserId, string? Email, string? Name);

public sealed record UpdateCalendarEventInput(string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool AllDay, string? Location, string? VideoLink, string? Rrule);

public sealed record CalendarEventDto(Guid Id, string Kind, string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool AllDay, string? Location, string? VideoLink, Guid? MatterId, string? Rrule, Guid? SeriesId, Guid? OrganizerId);

public sealed record CalendarItemDto(Guid Id, string ItemKind, string Title, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, bool AllDay, Guid? MatterId, string? Location, string? Status, bool IsLocked);

public sealed record EventReminderDto(Guid Id, string EventRefKind, Guid EventRefId, int OffsetMinutes, string Channel, string Status);

public sealed record FreeBusyResult(IReadOnlyDictionary<Guid, IReadOnlyList<BusyBlock>> BusyByUser);

public sealed record BusyBlock(DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Source);
