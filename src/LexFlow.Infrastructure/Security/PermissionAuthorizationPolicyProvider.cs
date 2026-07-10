using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace LexFlow.Infrastructure.Security;

/// <summary>
/// Turns any policy name of the form "Permission:module.action.scope" into a policy
/// requiring a <see cref="PermissionRequirement"/>, so [RequirePermission("...")] never
/// needs a matching AddPolicy(...) call registered up front for every permission string.
/// </summary>
public sealed class PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(RequirePermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var permission = policyName[RequirePermissionAttribute.PolicyPrefix.Length..];
            return new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}
