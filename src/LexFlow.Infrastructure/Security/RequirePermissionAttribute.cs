using Microsoft.AspNetCore.Authorization;

namespace LexFlow.Infrastructure.Security;

/// <summary>
/// Controller-action attribute for the module.action.scope model (PRD §20(4): "every
/// controller action attributed — CI check fails unattributed endpoints"). Maps to a
/// dynamically-built policy resolved by <see cref="PermissionAuthorizationPolicyProvider"/>.
/// Usage: [RequirePermission("matters.read.own")].
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public RequirePermissionAttribute(string permission)
    {
        Policy = PolicyPrefix + permission;
    }
}
