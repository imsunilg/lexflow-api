using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Sessions;

/// <summary>DELETE /api/v1/sessions/{id} (PRD §17, §20(11) session manager).</summary>
public sealed record RevokeSessionCommand(Guid SessionId) : IRequest;

public sealed class RevokeSessionCommandHandler(ISessionManagementService sessionManagementService, ICurrentUserService currentUser)
    : IRequestHandler<RevokeSessionCommand>
{
    public async Task Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
        => await sessionManagementService.RevokeSessionAsync(currentUser.TenantId!.Value, request.SessionId, cancellationToken);
}
