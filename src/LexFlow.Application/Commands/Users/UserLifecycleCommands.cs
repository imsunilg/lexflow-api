using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Users;

/// <summary>POST /api/v1/users/{id}/suspend.</summary>
public sealed record SuspendUserCommand(Guid UserId) : IRequest;

public sealed class SuspendUserCommandHandler(IUserManagementService userManagementService, ICurrentUserService currentUser) : IRequestHandler<SuspendUserCommand>
{
    public async Task Handle(SuspendUserCommand request, CancellationToken cancellationToken)
        => await userManagementService.SuspendAsync(currentUser.TenantId!.Value, request.UserId, cancellationToken);
}

/// <summary>POST /api/v1/users/{id}/reactivate.</summary>
public sealed record ReactivateUserCommand(Guid UserId) : IRequest;

public sealed class ReactivateUserCommandHandler(IUserManagementService userManagementService, ICurrentUserService currentUser) : IRequestHandler<ReactivateUserCommand>
{
    public async Task Handle(ReactivateUserCommand request, CancellationToken cancellationToken)
        => await userManagementService.ReactivateAsync(currentUser.TenantId!.Value, request.UserId, cancellationToken);
}

/// <summary>
/// POST /api/v1/users/{id}/deactivate. The deactivation wizard (PRD Module 14: "forces
/// reassignment of matters/tasks/leads and revokes sessions/tokens") — AC-U1/U3.
/// </summary>
public sealed record DeactivateUserCommand(Guid UserId, IReadOnlyList<ReassignmentEntry> Reassignments) : IRequest;

public sealed class DeactivateUserCommandHandler(IUserManagementService userManagementService, ICurrentUserService currentUser) : IRequestHandler<DeactivateUserCommand>
{
    public async Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
        => await userManagementService.DeactivateAsync(currentUser.TenantId!.Value, request.UserId, request.Reassignments, cancellationToken);
}
