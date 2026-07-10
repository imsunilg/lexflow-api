using System.Security.Cryptography;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using IcalEvent = Ical.Net.CalendarComponents.CalendarEvent;

namespace LexFlow.Infrastructure.Ops;

/// <summary>
/// Module 6 (Calendar). RRULE occurrences are expanded with Ical.Net (a maintained
/// RFC 5545 implementation) rather than hand-rolled recurrence math, materialized 24
/// months rolling per the Module 6 validation rule. ops.v_calendar_items (hearings,
/// matter_important_dates deadlines, tasks) is read-only from this side — validation
/// rule: "drag-reschedule of hearings forbidden ... calendar shows them locked."
/// </summary>
public sealed class CalendarService(LexFlowDbContext db) : ICalendarService
{
    private static readonly TimeSpan MaxHorizon = TimeSpan.FromDays(24 * 31);

    public async Task<CalendarEventDto> CreateEventAsync(Guid tenantId, Guid? actorId, CreateCalendarEventInput input, CancellationToken cancellationToken = default)
    {
        if (input.EndsAt <= input.StartsAt)
        {
            throw new Application.Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("endsAt", "End must be after start.")]);
        }

        if (input.EndsAt - input.StartsAt > TimeSpan.FromDays(14))
        {
            throw new Application.Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("endsAt", "Event duration cannot exceed 14 days.")]);
        }

        var calendarEvent = new CalendarEvent(tenantId, input.Kind, input.Title, input.StartsAt, input.EndsAt, input.AllDay, input.Location, input.VideoLink, input.MatterId, input.Rrule, seriesId: null, input.OrganizerId);
        await db.CalendarEvents.AddAsync(calendarEvent, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (input.Attendees is { Count: > 0 })
        {
            if (input.Attendees.Count > 100)
            {
                throw new Application.Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("attendees", "At most 100 attendees.")]);
            }

            foreach (var attendee in input.Attendees)
            {
                await db.EventAttendees.AddAsync(new EventAttendee(tenantId, calendarEvent.Id, attendee.UserId, attendee.Email, attendee.Name, isOrganizer: false), cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return ToDto(calendarEvent);
    }

    public async Task<CalendarEventDto?> GetByIdAsync(Guid tenantId, Guid eventId, CancellationToken cancellationToken = default)
    {
        var ev = await db.CalendarEvents.SingleOrDefaultAsync(e => e.TenantId == tenantId && e.Id == eventId, cancellationToken);
        return ev is null ? null : ToDto(ev);
    }

    public async Task<CalendarEventDto> UpdateAsync(Guid tenantId, Guid eventId, UpdateCalendarEventInput input, string scope, DateOnly? occurrenceDate, CancellationToken cancellationToken = default)
    {
        var ev = await db.CalendarEvents.SingleOrDefaultAsync(e => e.TenantId == tenantId && e.Id == eventId, cancellationToken)
            ?? throw new NotFoundException(nameof(CalendarEvent), eventId);

        if (scope == "occurrence")
        {
            if (!occurrenceDate.HasValue)
            {
                throw new Application.Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("occurrenceDate", "occurrenceDate is required when scope=occurrence.")]);
            }

            await db.RecurrenceExceptions.AddAsync(new RecurrenceException(tenantId, eventId, occurrenceDate.Value, "Modified", input.StartsAt, input.EndsAt), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return ToDto(ev);
        }

        ev.Update(input.Title, input.StartsAt, input.EndsAt, input.AllDay, input.Location, input.VideoLink, input.Rrule);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(ev);
    }

    public async Task DeleteAsync(Guid tenantId, Guid eventId, string scope, DateOnly? occurrenceDate, CancellationToken cancellationToken = default)
    {
        var ev = await db.CalendarEvents.SingleOrDefaultAsync(e => e.TenantId == tenantId && e.Id == eventId, cancellationToken)
            ?? throw new NotFoundException(nameof(CalendarEvent), eventId);

        if (scope == "occurrence" && occurrenceDate.HasValue)
        {
            await db.RecurrenceExceptions.AddAsync(new RecurrenceException(tenantId, eventId, occurrenceDate.Value, "Deleted", null, null), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        db.CalendarEvents.Remove(ev);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CalendarItemDto>> GetCalendarAsync(Guid tenantId, DateTimeOffset from, DateTimeOffset to, IReadOnlyCollection<string>? types, CancellationToken cancellationToken = default)
    {
        var items = new List<CalendarItemDto>();

        var nativeEvents = await db.CalendarEvents.Where(e => e.TenantId == tenantId).ToListAsync(cancellationToken);
        foreach (var ev in nativeEvents)
        {
            var exceptions = await db.RecurrenceExceptions.Where(x => x.TenantId == tenantId && x.EventId == ev.Id).ToListAsync(cancellationToken);
            foreach (var (start, end) in ExpandOccurrences(ev, exceptions, from, to))
            {
                items.Add(new CalendarItemDto(ev.Id, "Event", ev.Title, start, end, ev.AllDay, ev.MatterId, ev.Location, null, false));
            }
        }

        if (db.Database.IsRelational())
        {
            var viewRows = await db.CalendarItemRows
                .Where(r => r.TenantId == tenantId && r.ItemKind != "Event" && r.StartsAt >= from && r.StartsAt <= to)
                .ToListAsync(cancellationToken);
            items.AddRange(viewRows.Select(r => new CalendarItemDto(r.Id, r.ItemKind, r.Title, r.StartsAt, r.EndsAt, r.AllDay, r.MatterId, r.Location, r.Status, r.IsLocked)));
        }
        else
        {
            items.AddRange(await GetNonNativeItemsForInMemoryAsync(tenantId, from, to, cancellationToken));
        }

        if (types is { Count: > 0 })
        {
            items = items.Where(i => types.Contains(i.ItemKind)).ToList();
        }

        return items.OrderBy(i => i.StartsAt).ToList();
    }

    /// <summary>ops.v_calendar_items is a Postgres view — EF InMemory has no concept of it, so unit tests exercise the same union logic against the four source tables directly.</summary>
    private async Task<List<CalendarItemDto>> GetNonNativeItemsForInMemoryAsync(Guid tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var result = new List<CalendarItemDto>();

        var hearings = await db.Hearings.Where(h => h.TenantId == tenantId).ToListAsync(cancellationToken);
        foreach (var h in hearings)
        {
            var startsAt = new DateTimeOffset(h.Date.ToDateTime(h.Time ?? TimeOnly.MinValue), TimeSpan.Zero);
            if (startsAt >= from && startsAt <= to)
            {
                var courtCase = await db.CourtCases.SingleOrDefaultAsync(c => c.Id == h.CaseId, cancellationToken);
                result.Add(new CalendarItemDto(h.Id, "Hearing", h.Purpose ?? "Hearing", startsAt, null, false, courtCase?.MatterId, h.Courtroom, h.Status, true));
            }
        }

        var deadlines = await db.MatterImportantDates.Where(m => m.TenantId == tenantId && m.DueAt >= from && m.DueAt <= to).ToListAsync(cancellationToken);
        result.AddRange(deadlines.Select(m => new CalendarItemDto(m.Id, "Deadline", m.Title, m.DueAt, null, false, m.MatterId, null, m.SatisfiedAt is not null ? "Satisfied" : "Pending", false)));

        var tasks = await db.OpsTasks.Where(t => t.TenantId == tenantId && t.DueAt != null && t.DueAt >= from && t.DueAt <= to).ToListAsync(cancellationToken);
        result.AddRange(tasks.Select(t => new CalendarItemDto(t.Id, "Task", t.Title, t.DueAt!.Value, null, false, t.MatterId, null, t.Status, false)));

        return result;
    }

    /// <summary>Expands ev's RRULE (if any) into concrete occurrences over [from, to], capped at a 24-month rolling horizon from the event's own start, applying any RecurrenceException overrides/deletions.</summary>
    private static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> ExpandOccurrences(CalendarEvent ev, IReadOnlyList<RecurrenceException> exceptions, DateTimeOffset from, DateTimeOffset to)
    {
        var duration = ev.EndsAt - ev.StartsAt;

        if (string.IsNullOrWhiteSpace(ev.Rrule))
        {
            if (ev.StartsAt <= to && ev.EndsAt >= from)
            {
                yield return (ev.StartsAt, ev.EndsAt);
            }

            yield break;
        }

        var icalEvent = new IcalEvent
        {
            Start = new CalDateTime(ev.StartsAt.UtcDateTime, "UTC"),
            Duration = duration,
        };
        icalEvent.RecurrenceRules.Add(new RecurrencePattern(ev.Rrule));

        var horizonEnd = ev.StartsAt + MaxHorizon;
        var searchEnd = to < horizonEnd ? to : horizonEnd;
        if (searchEnd < from)
        {
            yield break;
        }

        var exceptionsByDate = exceptions.ToDictionary(e => e.OccurrenceDate);
        var occurrences = icalEvent.GetOccurrences(from.UtcDateTime, searchEnd.UtcDateTime);

        foreach (var occurrence in occurrences)
        {
            var start = new DateTimeOffset(occurrence.Period.StartTime.AsUtc, TimeSpan.Zero);
            var occurrenceDate = DateOnly.FromDateTime(start.UtcDateTime);

            if (exceptionsByDate.TryGetValue(occurrenceDate, out var exception))
            {
                if (exception.ExceptionType == "Deleted")
                {
                    continue;
                }

                if (exception.ExceptionType == "Modified" && exception.OverrideStartsAt.HasValue)
                {
                    yield return (exception.OverrideStartsAt.Value, exception.OverrideEndsAt ?? exception.OverrideStartsAt.Value.Add(duration));
                    continue;
                }
            }

            yield return (start, start.Add(duration));
        }
    }

    public async Task<EventReminderDto> AddReminderAsync(Guid tenantId, Guid eventId, int offsetMinutes, string channel, CancellationToken cancellationToken = default)
    {
        if (offsetMinutes is < 0 or > 129_600)
        {
            throw new Application.Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("offsetMinutes", "Reminder offsets must be between 0 and 90 days.")]);
        }

        var reminder = new EventReminder(tenantId, "event", eventId, offsetMinutes, channel);
        await db.EventReminders.AddAsync(reminder, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new EventReminderDto(reminder.Id, reminder.EventRefKind, reminder.EventRefId, reminder.OffsetMinutes, reminder.Channel, reminder.Status);
    }

    public async Task<FreeBusyResult> GetFreeBusyAsync(Guid tenantId, IReadOnlyList<Guid> userIds, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var busy = new Dictionary<Guid, IReadOnlyList<BusyBlock>> { };

        foreach (var userId in userIds)
        {
            var blocks = new List<BusyBlock>();

            var ownEvents = await db.CalendarEvents
                .Where(e => e.TenantId == tenantId && e.OrganizerId == userId)
                .ToListAsync(cancellationToken);
            foreach (var ev in ownEvents)
            {
                var exceptions = await db.RecurrenceExceptions.Where(x => x.TenantId == tenantId && x.EventId == ev.Id).ToListAsync(cancellationToken);
                blocks.AddRange(ExpandOccurrences(ev, exceptions, from, to).Select(o => new BusyBlock(o.Start, o.End, "Event")));
            }

            var hearings = await db.Hearings.Where(h => h.TenantId == tenantId && h.AssignedLawyerId == userId).ToListAsync(cancellationToken);
            blocks.AddRange(hearings
                .Select(h => new DateTimeOffset(h.Date.ToDateTime(h.Time ?? TimeOnly.MinValue), TimeSpan.Zero))
                .Where(start => start >= from && start <= to)
                .Select(start => new BusyBlock(start, start.AddHours(1), "Hearing")));

            busy[userId] = blocks;
        }

        return new FreeBusyResult(busy);
    }

    public async Task<string> GetOrCreateIcsTokenAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var existing = await db.CalendarIcsTokens.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.UserId == userId && t.RevokedAt == null, cancellationToken);
        if (existing is not null)
        {
            return existing.Token;
        }

        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = $"{tenantId:N}.{secret}";
        var icsToken = new CalendarIcsToken(tenantId, userId, token);
        await db.CalendarIcsTokens.AddAsync(icsToken, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task RevokeIcsTokenAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var tokens = await db.CalendarIcsTokens.Where(t => t.TenantId == tenantId && t.UserId == userId && t.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetIcsFeedAsync(string token, CancellationToken cancellationToken = default)
    {
        var dotIndex = token.IndexOf('.');
        if (dotIndex <= 0 || !Guid.TryParseExact(token[..dotIndex], "N", out var tenantId))
        {
            return null;
        }

        await db.SetTenantIdAsync(tenantId, cancellationToken);

        var icsToken = await db.CalendarIcsTokens.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Token == token && t.RevokedAt == null, cancellationToken);
        if (icsToken is null)
        {
            return null;
        }

        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow.AddMonths(3);
        var items = await GetCalendarAsync(tenantId, from, to, types: null, cancellationToken);

        var calendar = new Ical.Net.Calendar();
        foreach (var item in items)
        {
            calendar.Events.Add(new IcalEvent
            {
                Uid = item.Id.ToString(),
                Summary = item.Title,
                Start = new CalDateTime(item.StartsAt.UtcDateTime, "UTC"),
                End = new CalDateTime((item.EndsAt ?? item.StartsAt.AddHours(1)).UtcDateTime, "UTC"),
                Location = item.Location,
            });
        }

        return new CalendarSerializer().SerializeToString(calendar);
    }

    private static CalendarEventDto ToDto(CalendarEvent e) => new(e.Id, e.Kind, e.Title, e.StartsAt, e.EndsAt, e.AllDay, e.Location, e.VideoLink, e.MatterId, e.Rrule, e.SeriesId, e.OrganizerId);
}
