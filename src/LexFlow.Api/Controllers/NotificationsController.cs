using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Notifications;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Notifications;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>§22 Notification Matrix — the in-app inbox.</summary>
[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("notifications.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] bool unreadOnly, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<NotificationDto>>.Of(await mediator.Send(new GetNotificationsQuery(unreadOnly), cancellationToken)));

    [HttpPost("{id:guid}/read")]
    [RequirePermission("notifications.read.own")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new MarkNotificationReadCommand(id), cancellationToken);
        return NoContent();
    }
}
