using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Roles;

/// <summary>POST /api/v1/roles — custom role creation (PRD §17, §21, Module 14 Validation: "custom role cannot grant permissions its creator lacks").</summary>
public sealed record CreateRoleCommand(string Key, string Name, IReadOnlyList<Guid> PermissionIds) : IRequest<RoleDto>;

public sealed class CreateRoleCommandHandler(IRoleService roleService, ICurrentUserService currentUser) : IRequestHandler<CreateRoleCommand, RoleDto>
{
    public Task<RoleDto> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
        => roleService.CreateAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Key, request.Name, request.PermissionIds, cancellationToken);
}

/// <summary>PUT /api/v1/roles/{id}.</summary>
public sealed record UpdateRoleCommand(Guid RoleId, string Name, IReadOnlyList<Guid> PermissionIds) : IRequest<RoleDto>;

public sealed class UpdateRoleCommandHandler(IRoleService roleService, ICurrentUserService currentUser) : IRequestHandler<UpdateRoleCommand, RoleDto>
{
    public Task<RoleDto> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
        => roleService.UpdateAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.RoleId, request.Name, request.PermissionIds, cancellationToken);
}
