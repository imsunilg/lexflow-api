using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Users;

/// <summary>GET /api/v1/users/{id}.</summary>
public sealed record GetUserQuery(Guid UserId) : IRequest<UserDto>;

public sealed class GetUserQueryHandler(IUserManagementService userManagementService, ICurrentUserService currentUser) : IRequestHandler<GetUserQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken cancellationToken)
        => await userManagementService.GetByIdAsync(currentUser.TenantId!.Value, request.UserId, cancellationToken)
           ?? throw new NotFoundException("User", request.UserId);
}

/// <summary>GET /api/v1/users — list, backing the User Management screen.</summary>
public sealed record GetUsersQuery : IRequest<IReadOnlyList<UserDto>>;

public sealed class GetUsersQueryHandler(IUserManagementService userManagementService, ICurrentUserService currentUser)
    : IRequestHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    public Task<IReadOnlyList<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
        => userManagementService.GetAllAsync(currentUser.TenantId!.Value, cancellationToken);
}

/// <summary>GET /api/v1/users/{id}/unresolved-assignments — the checklist a deactivation would need reassigned, without performing it.</summary>
public sealed record GetUnresolvedAssignmentsQuery(Guid UserId) : IRequest<IReadOnlyList<UnresolvedAssignment>>;

public sealed class GetUnresolvedAssignmentsQueryHandler(IUserManagementService userManagementService, ICurrentUserService currentUser)
    : IRequestHandler<GetUnresolvedAssignmentsQuery, IReadOnlyList<UnresolvedAssignment>>
{
    public Task<IReadOnlyList<UnresolvedAssignment>> Handle(GetUnresolvedAssignmentsQuery request, CancellationToken cancellationToken)
        => userManagementService.GetUnresolvedAssignmentsAsync(currentUser.TenantId!.Value, request.UserId, cancellationToken);
}

/// <summary>
/// GET /api/v1/users/{id}/effective-permissions?resource= — AC-U2 trace view.
/// "resource" narrows to one module (e.g. "matters") when provided; the full
/// ownership-chain evaluation PRD §19.10's fn_can_access_matter describes for a
/// specific record is out of scope here (it depends on modules not yet built) —
/// this returns the granting rule chain for every permission in that module.
/// </summary>
public sealed record GetEffectivePermissionsQuery(Guid UserId, string? Resource) : IRequest<IReadOnlyList<EffectivePermissionExplanation>>;

public sealed class GetEffectivePermissionsQueryHandler(IPermissionService permissionService, ICurrentUserService currentUser)
    : IRequestHandler<GetEffectivePermissionsQuery, IReadOnlyList<EffectivePermissionExplanation>>
{
    public async Task<IReadOnlyList<EffectivePermissionExplanation>> Handle(GetEffectivePermissionsQuery request, CancellationToken cancellationToken)
    {
        var explanations = await permissionService.ExplainEffectivePermissionsAsync(request.UserId, currentUser.TenantId!.Value, cancellationToken);
        return string.IsNullOrWhiteSpace(request.Resource)
            ? explanations
            : explanations.Where(e => string.Equals(e.Module, request.Resource, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
