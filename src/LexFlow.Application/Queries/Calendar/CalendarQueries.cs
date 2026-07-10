using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Calendar;

/// <summary>GET /api/v1/calendar/events/{id}.</summary>
public sealed record GetCalendarEventQuery(Guid EventId) : IRequest<CalendarEventDto>;

public sealed class GetCalendarEventQueryHandler(ICalendarService calendarService, ICurrentUserService currentUser) : IRequestHandler<GetCalendarEventQuery, CalendarEventDto>
{
    public async Task<CalendarEventDto> Handle(GetCalendarEventQuery request, CancellationToken cancellationToken)
        => await calendarService.GetByIdAsync(currentUser.TenantId!.Value, request.EventId, cancellationToken)
           ?? throw new NotFoundException("CalendarEvent", request.EventId);
}

/// <summary>GET /api/v1/calendar?from=&amp;to=&amp;types=&amp;scope=. Merges native events (RRULE-expanded) with hearings/deadlines/tasks.</summary>
public sealed record GetCalendarQuery(DateTimeOffset From, DateTimeOffset To, IReadOnlyCollection<string>? Types) : IRequest<IReadOnlyList<CalendarItemDto>>;

public sealed class GetCalendarQueryHandler(ICalendarService calendarService, ICurrentUserService currentUser) : IRequestHandler<GetCalendarQuery, IReadOnlyList<CalendarItemDto>>
{
    public Task<IReadOnlyList<CalendarItemDto>> Handle(GetCalendarQuery request, CancellationToken cancellationToken)
        => calendarService.GetCalendarAsync(currentUser.TenantId!.Value, request.From, request.To, request.Types, cancellationToken);
}

/// <summary>GET /api/v1/calendar/freebusy?userIds=&amp;from=&amp;to=.</summary>
public sealed record GetFreeBusyQuery(IReadOnlyList<Guid> UserIds, DateTimeOffset From, DateTimeOffset To) : IRequest<FreeBusyResult>;

public sealed class GetFreeBusyQueryHandler(ICalendarService calendarService, ICurrentUserService currentUser) : IRequestHandler<GetFreeBusyQuery, FreeBusyResult>
{
    public Task<FreeBusyResult> Handle(GetFreeBusyQuery request, CancellationToken cancellationToken)
        => calendarService.GetFreeBusyAsync(currentUser.TenantId!.Value, request.UserIds, request.From, request.To, cancellationToken);
}
