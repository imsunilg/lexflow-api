using Microsoft.AspNetCore.Authorization;

namespace LexFlow.Infrastructure.Security;

/// <summary>
/// Authorization requirement for one "module.action.scope" permission string (PRD §21),
/// e.g. "matters.read.own". Parsed once at construction so PermissionHandler doesn't
/// re-split the string on every request.
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission)
    {
        Permission = permission;
        var segments = permission.Split('.');
        if (segments.Length != 3)
        {
            throw new ArgumentException(
                $"Permission '{permission}' must be in 'module.action.scope' form (PRD §20/§21).",
                nameof(permission));
        }

        Module = segments[0];
        Action = segments[1];
        Scope = segments[2];
    }

    public string Permission { get; }

    public string Module { get; }

    public string Action { get; }

    public string Scope { get; }
}
