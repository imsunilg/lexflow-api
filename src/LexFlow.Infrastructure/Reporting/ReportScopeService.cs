using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Reporting;

/// <summary>
/// Resolves the caller's highest granted scope for "reports.operational"/"reports.financial"
/// (PRD §21) into a concrete row-level predicate. Reuses the own&lt;team&lt;branch&lt;all scope-rank
/// convention from <c>PermissionEvaluator</c>, but unlike that class — which only answers "is this
/// endpoint call allowed" — this resolves the actual set of lawyer_key values (or branch) the
/// runner may see facts for, which is what every standard/custom report query filters by.
/// </summary>
public sealed class ReportScopeService(LexFlowDbContext db, IPermissionService permissionService) : IReportScopeService
{
    private static readonly Dictionary<string, int> ScopeRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["own"] = 1,
        ["team"] = 2,
        ["branch"] = 3,
        ["all"] = 4,
    };

    public async Task<ReportScope> ResolveAsync(Guid tenantId, Guid userId, string permissionArea, CancellationToken cancellationToken = default)
    {
        var permissions = await permissionService.GetEffectivePermissionsAsync(userId, tenantId, cancellationToken);

        var best = permissions
            .Where(p => string.Equals(p.Module, "reports", StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.Action, permissionArea, StringComparison.OrdinalIgnoreCase)
                && ScopeRank.ContainsKey(p.Scope))
            .OrderByDescending(p => ScopeRank[p.Scope])
            .Select(p => p.Scope)
            .FirstOrDefault();

        if (best is null)
        {
            return ReportScope.None();
        }

        switch (best.ToLowerInvariant())
        {
            case "all":
                return new ReportScope("all", null, null);

            case "branch":
            {
                var branchId = await db.Users
                    .Where(u => u.TenantId == tenantId && u.Id == userId)
                    .Select(u => u.BranchId)
                    .SingleOrDefaultAsync(cancellationToken);
                return new ReportScope("branch", null, branchId);
            }

            case "team":
            {
                var teamIds = await db.TeamMembers
                    .Where(m => m.TenantId == tenantId && m.UserId == userId)
                    .Select(m => m.TeamId)
                    .ToListAsync(cancellationToken);

                var teammateIds = await db.TeamMembers
                    .Where(m => m.TenantId == tenantId && teamIds.Contains(m.TeamId))
                    .Select(m => m.UserId)
                    .ToListAsync(cancellationToken);

                teammateIds.Add(userId);
                return new ReportScope("team", teammateIds.Distinct().ToList(), null);
            }

            default: // own
                return new ReportScope("own", [userId], null);
        }
    }
}
