using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Notifications;

/// <summary>POST /api/v1/notifications/{id}/read.</summary>
public sealed record MarkNotificationReadCommand(Guid NotificationId) : IRequest;

public sealed class MarkNotificationReadCommandHandler(INotificationService notificationService, ICurrentUserService currentUser) : IRequestHandler<MarkNotificationReadCommand>
{
    public async Task Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
        => await notificationService.MarkReadAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.NotificationId, cancellationToken);
}
