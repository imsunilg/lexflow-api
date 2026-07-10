using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Sessions;

/// <summary>GET /api/v1/users/{id}/sessions.</summary>
public sealed record GetUserSessionsQuery(Guid UserId) : IRequest<IReadOnlyList<SessionDto>>;

public sealed class GetUserSessionsQueryHandler(ISessionManagementService sessionManagementService, ICurrentUserService currentUser)
    : IRequestHandler<GetUserSessionsQuery, IReadOnlyList<SessionDto>>
{
    public Task<IReadOnlyList<SessionDto>> Handle(GetUserSessionsQuery request, CancellationToken cancellationToken)
        => sessionManagementService.GetUserSessionsAsync(currentUser.TenantId!.Value, request.UserId, cancellationToken);
}

/// <summary>GET /api/v1/login-history?userId=&amp;from= (PRD §17, §20(12)).</summary>
public sealed record GetLoginHistoryQuery(Guid? UserId, DateTimeOffset? From) : IRequest<IReadOnlyList<LoginHistoryDto>>;

public sealed class GetLoginHistoryQueryHandler(ISessionManagementService sessionManagementService, ICurrentUserService currentUser)
    : IRequestHandler<GetLoginHistoryQuery, IReadOnlyList<LoginHistoryDto>>
{
    public Task<IReadOnlyList<LoginHistoryDto>> Handle(GetLoginHistoryQuery request, CancellationToken cancellationToken)
        => sessionManagementService.GetLoginHistoryAsync(currentUser.TenantId!.Value, request.UserId, request.From, cancellationToken);
}
