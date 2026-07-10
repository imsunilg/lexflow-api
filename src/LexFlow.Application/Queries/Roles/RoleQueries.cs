using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Roles;

/// <summary>GET /api/v1/roles.</summary>
public sealed record GetRolesQuery : IRequest<IReadOnlyList<RoleDto>>;

public sealed class GetRolesQueryHandler(IRoleService roleService, ICurrentUserService currentUser) : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    public Task<IReadOnlyList<RoleDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
        => roleService.GetAllAsync(currentUser.TenantId!.Value, cancellationToken);
}

/// <summary>GET /api/v1/roles/{id}.</summary>
public sealed record GetRoleQuery(Guid RoleId) : IRequest<RoleDto>;

public sealed class GetRoleQueryHandler(IRoleService roleService, ICurrentUserService currentUser) : IRequestHandler<GetRoleQuery, RoleDto>
{
    public async Task<RoleDto> Handle(GetRoleQuery request, CancellationToken cancellationToken)
        => await roleService.GetByIdAsync(currentUser.TenantId!.Value, request.RoleId, cancellationToken)
           ?? throw new NotFoundException("Role", request.RoleId);
}

/// <summary>GET /api/v1/permissions/catalog (PRD §17, Module 14 Error Handling: "catalog is server-served").</summary>
public sealed record GetPermissionsCatalogQuery : IRequest<IReadOnlyList<PermissionCatalogItem>>;

public sealed class GetPermissionsCatalogQueryHandler(IPermissionService permissionService, ICurrentUserService currentUser)
    : IRequestHandler<GetPermissionsCatalogQuery, IReadOnlyList<PermissionCatalogItem>>
{
    public Task<IReadOnlyList<PermissionCatalogItem>> Handle(GetPermissionsCatalogQuery request, CancellationToken cancellationToken)
        => permissionService.GetCatalogAsync(currentUser.TenantId!.Value, cancellationToken);
}
