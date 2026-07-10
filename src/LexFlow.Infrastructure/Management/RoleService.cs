using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Management;

/// <summary>Module 14 Roles CRUD (PRD §17, §21).</summary>
public sealed class RoleService(LexFlowDbContext db, IPermissionService permissionService) : IRoleService
{
    public async Task<IReadOnlyList<RoleDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var roles = await db.Roles.Where(r => r.TenantId == tenantId).OrderBy(r => r.Name).ToListAsync(cancellationToken);
        var result = new List<RoleDto>(roles.Count);
        foreach (var role in roles)
        {
            result.Add(await ToDtoAsync(role, cancellationToken));
        }

        return result;
    }

    public async Task<RoleDto?> GetByIdAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == roleId, cancellationToken);
        return role is null ? null : await ToDtoAsync(role, cancellationToken);
    }

    public async Task<RoleDto> CreateAsync(Guid tenantId, Guid createdBy, string key, string name, IReadOnlyList<Guid> permissionIds, CancellationToken cancellationToken = default)
    {
        await EnsureCreatorHoldsAllAsync(tenantId, createdBy, permissionIds, cancellationToken);

        var role = new Role(tenantId, key, name, isSystem: false);
        await db.Roles.AddAsync(role, cancellationToken);
        await AddGrantsAsync(tenantId, role.Id, permissionIds, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(role, cancellationToken);
    }

    public async Task<RoleDto> UpdateAsync(Guid tenantId, Guid createdBy, Guid roleId, string name, IReadOnlyList<Guid> permissionIds, CancellationToken cancellationToken = default)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == roleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), roleId);

        if (role.IsSystem)
        {
            throw new ForbiddenAccessException();
        }

        await EnsureCreatorHoldsAllAsync(tenantId, createdBy, permissionIds, cancellationToken);

        role.Rename(name);

        var existingGrants = await db.RolePermissions.Where(rp => rp.TenantId == tenantId && rp.RoleId == roleId).ToListAsync(cancellationToken);
        db.RolePermissions.RemoveRange(existingGrants);
        await AddGrantsAsync(tenantId, roleId, permissionIds, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(role, cancellationToken);
    }

    private async Task EnsureCreatorHoldsAllAsync(Guid tenantId, Guid createdBy, IReadOnlyList<Guid> permissionIds, CancellationToken cancellationToken)
    {
        if (permissionIds.Count == 0)
        {
            return;
        }

        var creatorPermissionKeys = (await permissionService.GetEffectivePermissionsAsync(createdBy, tenantId, cancellationToken))
            .Select(p => p.Key)
            .ToHashSet();

        var requestedKeys = await db.Permissions
            .Where(p => p.TenantId == tenantId && permissionIds.Contains(p.Id))
            .Select(p => p.Key)
            .ToListAsync(cancellationToken);

        if (requestedKeys.Any(key => !creatorPermissionKeys.Contains(key)))
        {
            throw new ForbiddenAccessException();
        }
    }

    private async Task AddGrantsAsync(Guid tenantId, Guid roleId, IReadOnlyList<Guid> permissionIds, CancellationToken cancellationToken)
    {
        foreach (var permissionId in permissionIds)
        {
            await db.RolePermissions.AddAsync(new RolePermission(tenantId, roleId, permissionId), cancellationToken);
        }
    }

    private async Task<RoleDto> ToDtoAsync(Role role, CancellationToken cancellationToken)
    {
        var permissionIds = await db.RolePermissions
            .Where(rp => rp.TenantId == role.TenantId && rp.RoleId == role.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync(cancellationToken);

        return new RoleDto(role.Id, role.Key, role.Name, role.IsSystem, permissionIds);
    }
}
