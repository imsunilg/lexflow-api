using Hangfire.Dashboard;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Security;

namespace LexFlow.Api.Security;

/// <summary>
/// Gates the Hangfire Dashboard (/hangfire) behind the same "settings.manage.all"
/// permission SettingsController itself requires. Re-resolves effective permissions via
/// IPermissionService rather than trusting JWT claims — same rationale as PermissionHandler:
/// authorization must never be stale beyond the permission cache TTL, especially for a
/// surface that can requeue/delete background jobs across every tenant.
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var userIdClaim = httpContext.User.FindFirst("sub")?.Value;
        var tenantIdClaim = httpContext.User.FindFirst("tenant")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId) || !Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            return false;
        }

        var permissionService = httpContext.RequestServices.GetRequiredService<IPermissionService>();
        var effectivePermissions = permissionService.GetEffectivePermissionsAsync(userId, tenantId).GetAwaiter().GetResult();
        return PermissionEvaluator.Grants(effectivePermissions, "settings", "manage", "all");
    }
}
