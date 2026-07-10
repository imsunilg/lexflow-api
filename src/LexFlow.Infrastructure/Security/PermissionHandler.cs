using LexFlow.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace LexFlow.Infrastructure.Security;

/// <summary>
/// Evaluates a <see cref="PermissionRequirement"/> against the caller's effective
/// permission set. Deliberately re-resolves permissions via <see cref="IPermissionService"/>
/// on every check rather than trusting the "perm" claims embedded in the JWT at issuance —
/// PRD Module 14 Edge Cases: "sensitive checks always DB-verified" (permission cache
/// invalidation is best-effort within 60 s, but authorization itself must not be stale
/// for longer than the cache TTL, and never trust a token that outlives a permission change).
/// </summary>
public sealed class PermissionHandler(IPermissionService permissionService) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst("sub")?.Value;
        var tenantIdClaim = context.User.FindFirst("tenant")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId) || !Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            return;
        }

        var effectivePermissions = await permissionService.GetEffectivePermissionsAsync(userId, tenantId);

        if (PermissionEvaluator.Grants(effectivePermissions, requirement.Module, requirement.Action, requirement.Scope))
        {
            context.Succeed(requirement);
        }
    }
}
