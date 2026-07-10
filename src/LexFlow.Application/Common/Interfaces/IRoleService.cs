namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 14 Roles CRUD (PRD §17: GET/POST/PUT /api/v1/roles). System roles are read-only (§14 Roles: system + custom Enterprise roles).</summary>
public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<RoleDto?> GetByIdAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>Custom role creation. Rejects any permission the creator's own effective set doesn't include (PRD Module 14 Validation: "custom role cannot grant permissions its creator lacks").</summary>
    Task<RoleDto> CreateAsync(Guid tenantId, Guid createdBy, string key, string name, IReadOnlyList<Guid> permissionIds, CancellationToken cancellationToken = default);

    Task<RoleDto> UpdateAsync(Guid tenantId, Guid createdBy, Guid roleId, string name, IReadOnlyList<Guid> permissionIds, CancellationToken cancellationToken = default);
}

public sealed record RoleDto(Guid Id, string Key, string Name, bool IsSystem, IReadOnlyList<Guid> PermissionIds);
