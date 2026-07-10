using LexFlow.Application.Common.Interfaces;

namespace LexFlow.Infrastructure.Security;

/// <summary>
/// Pure scope-hierarchy logic for the "module.action.scope" model (PRD §21):
/// own &lt; team &lt; branch &lt; all. A grant satisfies a requirement for the same
/// module+action whenever the grant's scope rank is greater than or equal to the
/// requirement's scope rank. Kept as a standalone static class (rather than inlined
/// into PermissionHandler) so unit tests can exercise the hierarchy rule directly.
/// </summary>
public static class PermissionEvaluator
{
    private static readonly Dictionary<string, int> ScopeRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["own"] = 1,
        ["team"] = 2,
        ["branch"] = 3,
        ["all"] = 4,
    };

    public static bool Grants(IEnumerable<EffectivePermission> effectivePermissions, string module, string action, string requiredScope)
    {
        var requiredRank = Rank(requiredScope);

        foreach (var permission in effectivePermissions)
        {
            if (!string.Equals(permission.Module, module, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(permission.Action, action, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (requiredRank is null || Rank(permission.Scope) is not { } grantedRank)
            {
                // Either side uses a scope outside own/team/branch/all (e.g. "special") —
                // only an exact match is meaningful there.
                if (string.Equals(permission.Scope, requiredScope, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            if (grantedRank >= requiredRank)
            {
                return true;
            }
        }

        return false;
    }

    private static int? Rank(string scope) => ScopeRank.TryGetValue(scope, out var rank) ? rank : null;
}
