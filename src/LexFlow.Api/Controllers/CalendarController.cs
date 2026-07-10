using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Calendar;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Calendar;
using LexFlow.Infrastructure.Ops;
using LexFlow.Infrastructure.Persistence;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 6 (Calendar) — PRD §17.</summary>
[ApiController]
[Route("api/v1/calendar")]
public sealed class CalendarController(IMediator mediator) : ControllerBase
{
    /// <summary>GET /api/v1/calendar?from=&amp;to=&amp;types=&amp;scope= (expanded occurrences).</summary>
    [HttpGet]
    [RequirePermission("calendar.read.own")]
    public async Task<IActionResult> Get([FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, [FromQuery] string? types, CancellationToken cancellationToken)
    {
        var typeList = string.IsNullOrWhiteSpace(types) ? null : types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Ok(ApiResponse<IReadOnlyList<CalendarItemDto>>.Of(await mediator.Send(new GetCalendarQuery(from, to, typeList), cancellationToken)));
    }

    [HttpPost("events")]
    [RequirePermission("calendar.manage.own")]
    public async Task<IActionResult> CreateEvent([FromBody] CreateCalendarEventRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<CalendarEventDto>.Of(await mediator.Send(new CreateCalendarEventCommand(request.Kind, request.Title, request.StartsAt, request.EndsAt, request.AllDay, request.Location, request.VideoLink, request.MatterId, request.Rrule, request.OrganizerId, request.Attendees), cancellationToken)));

    [HttpGet("events/{id:guid}")]
    [RequirePermission("calendar.read.own")]
    public async Task<IActionResult> GetEvent(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<CalendarEventDto>.Of(await mediator.Send(new GetCalendarEventQuery(id), cancellationToken)));

    /// <summary>AC-CAL3: ?scope=occurrence|series&amp;occurrenceDate=.</summary>
    [HttpPut("events/{id:guid}")]
    [RequirePermission("calendar.manage.own")]
    public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] UpdateCalendarEventRequest request, [FromQuery] string scope = "series", [FromQuery] DateOnly? occurrenceDate = null, CancellationToken cancellationToken = default)
        => Ok(ApiResponse<CalendarEventDto>.Of(await mediator.Send(new UpdateCalendarEventCommand(id, request.Title, request.StartsAt, request.EndsAt, request.AllDay, request.Location, request.VideoLink, request.Rrule, scope, occurrenceDate), cancellationToken)));

    [HttpDelete("events/{id:guid}")]
    [RequirePermission("calendar.manage.own")]
    public async Task<IActionResult> DeleteEvent(Guid id, [FromQuery] string scope = "series", [FromQuery] DateOnly? occurrenceDate = null, CancellationToken cancellationToken = default)
    {
        await mediator.Send(new DeleteCalendarEventCommand(id, scope, occurrenceDate), cancellationToken);
        return NoContent();
    }

    [HttpPost("events/{id:guid}/reminders")]
    [RequirePermission("calendar.manage.own")]
    public async Task<IActionResult> AddReminder(Guid id, [FromBody] AddEventReminderRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<EventReminderDto>.Of(await mediator.Send(new AddEventReminderCommand(id, request.OffsetMinutes, request.Channel), cancellationToken)));

    [HttpGet("freebusy")]
    [RequirePermission("calendar.read.team")]
    public async Task<IActionResult> GetFreeBusy([FromQuery] string userIds, [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, CancellationToken cancellationToken)
    {
        var ids = userIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList();
        return Ok(ApiResponse<FreeBusyResult>.Of(await mediator.Send(new GetFreeBusyQuery(ids, from, to), cancellationToken)));
    }

    [HttpGet("ics/token")]
    [RequirePermission("calendar.read.own")]
    public async Task<IActionResult> GetIcsToken(CancellationToken cancellationToken)
        => Ok(ApiResponse<string>.Of(await mediator.Send(new GetOrCreateIcsTokenCommand(), cancellationToken)));

    [HttpDelete("ics/token")]
    [RequirePermission("calendar.read.own")]
    public async Task<IActionResult> RevokeIcsToken(CancellationToken cancellationToken)
    {
        await mediator.Send(new RevokeIcsTokenCommand(), cancellationToken);
        return NoContent();
    }

    [HttpPost("sync/{provider}/connect")]
    [RequirePermission("calendar.manage.own")]
    public async Task<IActionResult> ConnectExternal(string provider, [FromBody] ConnectExternalCalendarRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<string>.Of(await mediator.Send(new ConnectExternalCalendarCommand(provider, request.RedirectUri), cancellationToken)));

    [HttpPost("sync/{provider}/callback")]
    [RequirePermission("calendar.manage.own")]
    public async Task<IActionResult> HandleOAuthCallback(string provider, [FromBody] CalendarOAuthCallbackRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<Guid>.Of(await mediator.Send(new HandleCalendarOAuthCallbackCommand(provider, request.Code, request.RedirectUri), cancellationToken)));

    /// <summary>AC-CAL4: removeRemoteEvents (user choice: remove|keep) deletes LexFlow-created remote events before purging tokens.</summary>
    [HttpDelete("sync/{provider}/disconnect/{accountId:guid}")]
    [RequirePermission("calendar.manage.own")]
    public async Task<IActionResult> Disconnect(string provider, Guid accountId, [FromQuery] bool removeRemoteEvents, CancellationToken cancellationToken)
    {
        await mediator.Send(new DisconnectExternalCalendarCommand(provider, accountId, removeRemoteEvents), cancellationToken);
        return NoContent();
    }

    [HttpPost("sync/{provider}/push/{accountId:guid}/{eventId:guid}")]
    [RequirePermission("calendar.manage.own")]
    public async Task<IActionResult> Push(string provider, Guid accountId, Guid eventId, CancellationToken cancellationToken)
    {
        await mediator.Send(new PushCalendarEventCommand(provider, accountId, eventId), cancellationToken);
        return NoContent();
    }
}

/// <summary>
/// Public/unauthenticated ICS feed and provider webhooks — PRD §17:
/// GET /api/v1/calendar/ics/{secret}.ics, POST /api/v1/calendar/sync/webhook/{google|microsoft}.
/// Separate controller (not nested under CalendarController) so [AllowAnonymous] doesn't
/// leak onto the authenticated calendar endpoints above.
/// </summary>
[ApiController]
[Route("api/v1")]
[AllowAnonymous]
public sealed class PublicCalendarController(ICalendarService calendarService, LexFlowDbContext dbContext) : ControllerBase
{
    [HttpGet("calendar/ics/{secret}.ics")]
    public async Task<IActionResult> GetIcsFeed(string secret, CancellationToken cancellationToken)
    {
        var ics = await calendarService.GetIcsFeedAsync(secret, cancellationToken);
        return ics is null ? NotFound() : Content(ics, "text/calendar");
    }

    /// <summary>
    /// Provider push-channel notification -> triggers an incremental pull. The tenant/account
    /// id must ride in the callback URL (same "embed the tenant id" pattern used by every
    /// other unauthenticated endpoint in this codebase — RLS needs app.tenant_id before any
    /// query, and there's no JWT on a provider-called webhook to source one from).
    /// </summary>
    [HttpPost("calendar/sync/webhook/{provider}/{tenantId:guid}/{accountId:guid}")]
    public async Task<IActionResult> HandleWebhook(string provider, Guid tenantId, Guid accountId, [FromServices] IEnumerable<ICalendarSync> syncProviders, CancellationToken cancellationToken)
    {
        await dbContext.SetTenantIdAsync(tenantId, cancellationToken);
        var syncProvider = syncProviders.SingleOrDefault(p => string.Equals(p.Provider, provider, StringComparison.OrdinalIgnoreCase));
        if (syncProvider is not null)
        {
            await syncProvider.PullChangesAsync(tenantId, accountId, cancellationToken);
        }

        return Ok();
    }
}

public sealed record CreateCalendarEventRequest(string Kind, string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool AllDay, string? Location, string? VideoLink, Guid? MatterId, string? Rrule, Guid? OrganizerId, IReadOnlyList<AttendeeInput>? Attendees);

public sealed record UpdateCalendarEventRequest(string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool AllDay, string? Location, string? VideoLink, string? Rrule);

public sealed record AddEventReminderRequest(int OffsetMinutes, string Channel);

public sealed record ConnectExternalCalendarRequest(string RedirectUri);

public sealed record CalendarOAuthCallbackRequest(string Code, string RedirectUri);
