using System.Text.Json;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace LexFlow.Infrastructure.Security;

/// <summary>
/// Resolves effective permissions from core.user_roles/role_permissions plus
/// core.user_permission_grants (PRD §21: primary role + ad-hoc grants), cached in
/// Redis for 60 s (PRD §20(4), Module 14 Edge Cases: role-edit invalidation ≤ 60 s).
/// </summary>
public sealed class PermissionService(LexFlowDbContext db, IConnectionMultiplexer redis) : IPermissionService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyCollection<EffectivePermission>> GetEffectivePermissionsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var cache = redis.GetDatabase();
        var cacheKey = CacheKey(tenantId, userId);

        var cached = await cache.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            var deserialized = JsonSerializer.Deserialize<List<EffectivePermission>>(cached!);
            if (deserialized is not null)
            {
                return deserialized;
            }
        }

        var viaRole =
            from userRole in db.UserRoles
            join rolePermission in db.RolePermissions on userRole.RoleId equals rolePermission.RoleId
            join permission in db.Permissions on rolePermission.PermissionId equals permission.Id
            where userRole.UserId == userId && userRole.TenantId == tenantId
            select new { permission.Key, permission.Module, permission.Action, permission.Scope };

        var viaGrant =
            from grant in db.UserPermissionGrants
            join permission in db.Permissions on grant.PermissionId equals permission.Id
            where grant.UserId == userId && grant.TenantId == tenantId
            select new { permission.Key, permission.Module, permission.Action, permission.Scope };

        var rows = await viaRole.Union(viaGrant).ToListAsync(cancellationToken);
        var result = rows
            .Select(r => new EffectivePermission(r.Key, r.Module, r.Action, r.Scope))
            .ToList();

        await cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(result), CacheTtl);
        return result;
    }

    public async Task InvalidateAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var cache = redis.GetDatabase();
        await cache.KeyDeleteAsync(CacheKey(tenantId, userId));
    }

    public async Task<IReadOnlyList<PermissionCatalogItem>> GetCatalogAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var permissions = await db.Permissions
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Module).ThenBy(p => p.Action).ThenBy(p => p.Scope)
            .ToListAsync(cancellationToken);

        return permissions
            .Select(p => new PermissionCatalogItem(p.Id, p.Key, p.Module, p.Action, p.Scope, p.Label))
            .ToList();
    }

    public async Task<IReadOnlyList<EffectivePermissionExplanation>> ExplainEffectivePermissionsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // AC-U2: bypasses the Redis cache and re-derives from source tables so the
        // trace view always reflects the current, DB-verified grant chain (Module 14
        // Edge Cases: "sensitive checks always DB-verified").
        var viaRole =
            from userRole in db.UserRoles
            join role in db.Roles on userRole.RoleId equals role.Id
            join rolePermission in db.RolePermissions on userRole.RoleId equals rolePermission.RoleId
            join permission in db.Permissions on rolePermission.PermissionId equals permission.Id
            where userRole.UserId == userId && userRole.TenantId == tenantId
            select new
            {
                permission.Key,
                permission.Module,
                permission.Action,
                permission.Scope,
                SourceType = "role",
                SourceId = role.Id,
                SourceName = role.Name,
            };

        var viaGrant =
            from grant in db.UserPermissionGrants
            join permission in db.Permissions on grant.PermissionId equals permission.Id
            where grant.UserId == userId && grant.TenantId == tenantId
            select new
            {
                permission.Key,
                permission.Module,
                permission.Action,
                permission.Scope,
                SourceType = "direct_grant",
                SourceId = permission.Id,
                SourceName = permission.Label ?? permission.Key,
            };

        var rows = await viaRole.Concat(viaGrant).ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => new { r.Key, r.Module, r.Action, r.Scope })
            .Select(g => new EffectivePermissionExplanation(
                g.Key.Key,
                g.Key.Module,
                g.Key.Action,
                g.Key.Scope,
                g.Select(x => new GrantSource(x.SourceType, x.SourceId, x.SourceName)).Distinct().ToList()))
            .OrderBy(e => e.Module).ThenBy(e => e.Action).ThenBy(e => e.Scope)
            .ToList();
    }

    private static string CacheKey(Guid tenantId, Guid userId) => $"perm:{tenantId:N}:{userId:N}";
}
