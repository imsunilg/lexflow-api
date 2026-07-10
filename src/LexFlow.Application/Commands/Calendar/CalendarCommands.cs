using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Calendar;

/// <summary>POST /api/v1/calendar/events.</summary>
public sealed record CreateCalendarEventCommand(string Kind, string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool AllDay, string? Location, string? VideoLink, Guid? MatterId, string? Rrule, Guid? OrganizerId, IReadOnlyList<AttendeeInput>? Attendees) : IRequest<CalendarEventDto>;

public sealed class CreateCalendarEventCommandHandler(ICalendarService calendarService, ICurrentUserService currentUser) : IRequestHandler<CreateCalendarEventCommand, CalendarEventDto>
{
    public Task<CalendarEventDto> Handle(CreateCalendarEventCommand request, CancellationToken cancellationToken)
        => calendarService.CreateEventAsync(currentUser.TenantId!.Value, currentUser.UserId, new CreateCalendarEventInput(request.Kind, request.Title, request.StartsAt, request.EndsAt, request.AllDay, request.Location, request.VideoLink, request.MatterId, request.Rrule, request.OrganizerId, request.Attendees), cancellationToken);
}

/// <summary>PUT /api/v1/calendar/events/{id}?scope=occurrence|series&amp;occurrenceDate=. AC-CAL3.</summary>
public sealed record UpdateCalendarEventCommand(Guid EventId, string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool AllDay, string? Location, string? VideoLink, string? Rrule, string Scope, DateOnly? OccurrenceDate) : IRequest<CalendarEventDto>;

public sealed class UpdateCalendarEventCommandHandler(ICalendarService calendarService, ICurrentUserService currentUser) : IRequestHandler<UpdateCalendarEventCommand, CalendarEventDto>
{
    public Task<CalendarEventDto> Handle(UpdateCalendarEventCommand request, CancellationToken cancellationToken)
        => calendarService.UpdateAsync(currentUser.TenantId!.Value, request.EventId, new UpdateCalendarEventInput(request.Title, request.StartsAt, request.EndsAt, request.AllDay, request.Location, request.VideoLink, request.Rrule), request.Scope, request.OccurrenceDate, cancellationToken);
}

/// <summary>DELETE /api/v1/calendar/events/{id}?scope=occurrence|series&amp;occurrenceDate=.</summary>
public sealed record DeleteCalendarEventCommand(Guid EventId, string Scope, DateOnly? OccurrenceDate) : IRequest;

public sealed class DeleteCalendarEventCommandHandler(ICalendarService calendarService, ICurrentUserService currentUser) : IRequestHandler<DeleteCalendarEventCommand>
{
    public async Task Handle(DeleteCalendarEventCommand request, CancellationToken cancellationToken)
        => await calendarService.DeleteAsync(currentUser.TenantId!.Value, request.EventId, request.Scope, request.OccurrenceDate, cancellationToken);
}

/// <summary>POST /api/v1/calendar/events/{id}/reminders.</summary>
public sealed record AddEventReminderCommand(Guid EventId, int OffsetMinutes, string Channel) : IRequest<EventReminderDto>;

public sealed class AddEventReminderCommandHandler(ICalendarService calendarService, ICurrentUserService currentUser) : IRequestHandler<AddEventReminderCommand, EventReminderDto>
{
    public Task<EventReminderDto> Handle(AddEventReminderCommand request, CancellationToken cancellationToken)
        => calendarService.AddReminderAsync(currentUser.TenantId!.Value, request.EventId, request.OffsetMinutes, request.Channel, cancellationToken);
}

/// <summary>POST /api/v1/calendar/ics/token — (re)issue this user's ICS secret URL.</summary>
public sealed record GetOrCreateIcsTokenCommand : IRequest<string>;

public sealed class GetOrCreateIcsTokenCommandHandler(ICalendarService calendarService, ICurrentUserService currentUser) : IRequestHandler<GetOrCreateIcsTokenCommand, string>
{
    public Task<string> Handle(GetOrCreateIcsTokenCommand request, CancellationToken cancellationToken)
        => calendarService.GetOrCreateIcsTokenAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, cancellationToken);
}

/// <summary>DELETE /api/v1/calendar/ics/token — revoke this user's ICS secret URL.</summary>
public sealed record RevokeIcsTokenCommand : IRequest;

public sealed class RevokeIcsTokenCommandHandler(ICalendarService calendarService, ICurrentUserService currentUser) : IRequestHandler<RevokeIcsTokenCommand>
{
    public async Task Handle(RevokeIcsTokenCommand request, CancellationToken cancellationToken)
        => await calendarService.RevokeIcsTokenAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, cancellationToken);
}

/// <summary>POST /api/v1/calendar/sync/{provider}/connect (OAuth start) — returns the provider's consent-screen URL.</summary>
public sealed record ConnectExternalCalendarCommand(string Provider, string RedirectUri) : IRequest<string>;

public sealed class ConnectExternalCalendarCommandHandler(IEnumerable<ICalendarSync> syncProviders, ICurrentUserService currentUser) : IRequestHandler<ConnectExternalCalendarCommand, string>
{
    public Task<string> Handle(ConnectExternalCalendarCommand request, CancellationToken cancellationToken)
    {
        var provider = ResolveProvider(syncProviders, request.Provider);
        return Task.FromResult(provider.GetAuthorizationUrl(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.RedirectUri));
    }

    internal static ICalendarSync ResolveProvider(IEnumerable<ICalendarSync> providers, string name)
        => providers.SingleOrDefault(p => string.Equals(p.Provider, name, StringComparison.OrdinalIgnoreCase))
           ?? throw new Common.Exceptions.NotFoundException("CalendarSyncProvider", name);
}

/// <summary>POST /api/v1/calendar/sync/{provider}/callback {code, redirectUri} — OAuth callback.</summary>
public sealed record HandleCalendarOAuthCallbackCommand(string Provider, string Code, string RedirectUri) : IRequest<Guid>;

public sealed class HandleCalendarOAuthCallbackCommandHandler(IEnumerable<ICalendarSync> syncProviders, ICurrentUserService currentUser) : IRequestHandler<HandleCalendarOAuthCallbackCommand, Guid>
{
    public Task<Guid> Handle(HandleCalendarOAuthCallbackCommand request, CancellationToken cancellationToken)
    {
        var provider = ConnectExternalCalendarCommandHandler.ResolveProvider(syncProviders, request.Provider);
        return provider.HandleOAuthCallbackAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Code, request.RedirectUri, cancellationToken);
    }
}

/// <summary>DELETE /api/v1/calendar/sync/{provider}/disconnect/{accountId}?removeRemoteEvents=. AC-CAL4.</summary>
public sealed record DisconnectExternalCalendarCommand(string Provider, Guid AccountId, bool RemoveRemoteEvents) : IRequest;

public sealed class DisconnectExternalCalendarCommandHandler(IEnumerable<ICalendarSync> syncProviders, ICurrentUserService currentUser) : IRequestHandler<DisconnectExternalCalendarCommand>
{
    public async Task Handle(DisconnectExternalCalendarCommand request, CancellationToken cancellationToken)
    {
        var provider = ConnectExternalCalendarCommandHandler.ResolveProvider(syncProviders, request.Provider);
        await provider.DisconnectAsync(currentUser.TenantId!.Value, request.AccountId, request.RemoveRemoteEvents, cancellationToken);
    }
}

/// <summary>POST /api/v1/calendar/sync/{provider}/push/{eventId}/{accountId} — push a LexFlow-born event out (AC-CAL1's outbound half).</summary>
public sealed record PushCalendarEventCommand(string Provider, Guid AccountId, Guid EventId) : IRequest;

public sealed class PushCalendarEventCommandHandler(IEnumerable<ICalendarSync> syncProviders, ICurrentUserService currentUser) : IRequestHandler<PushCalendarEventCommand>
{
    public async Task Handle(PushCalendarEventCommand request, CancellationToken cancellationToken)
    {
        var provider = ConnectExternalCalendarCommandHandler.ResolveProvider(syncProviders, request.Provider);
        await provider.PushEventAsync(currentUser.TenantId!.Value, request.AccountId, request.EventId, cancellationToken);
    }
}
