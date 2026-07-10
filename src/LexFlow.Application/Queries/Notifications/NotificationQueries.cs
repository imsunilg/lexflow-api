using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Notifications;

/// <summary>GET /api/v1/notifications?unreadOnly=.</summary>
public sealed record GetNotificationsQuery(bool UnreadOnly) : IRequest<IReadOnlyList<NotificationDto>>;

public sealed class GetNotificationsQueryHandler(INotificationService notificationService, ICurrentUserService currentUser) : IRequestHandler<GetNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    public Task<IReadOnlyList<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
        => notificationService.GetForUserAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.UnreadOnly, cancellationToken);
}
