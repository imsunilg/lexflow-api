using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Ai;

/// <summary>See IAiRetrievalGuard's own doc comment for the per-source-kind reuse rationale.</summary>
public sealed class AiRetrievalGuard(LexFlowDbContext db, IPermissionService permissionService, IDocumentService documentService, IKbArticleService kbArticleService) : IAiRetrievalGuard
{
    private static readonly Dictionary<string, int> ScopeRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["own"] = 1,
        ["team"] = 2,
        ["branch"] = 3,
        ["all"] = 4,
    };

    public async Task<bool> CanAccessAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, string sourceKind, Guid sourceId, CancellationToken cancellationToken = default) =>
        sourceKind switch
        {
            "Document" => await CanAccessDocumentAsync(tenantId, sourceId, callerPermissions, cancellationToken),
            "Matter" => await CanAccessMatterAsync(tenantId, callerId, sourceId, cancellationToken),
            "KbArticle" => await kbArticleService.GetAsync(tenantId, sourceId, callerId, callerPermissions.Contains("kb.review.all"), cancellationToken) is not null,
            "KbJudgment" => callerPermissions.Contains("kb.read.all") && await db.KbJudgments.AnyAsync(j => j.TenantId == tenantId && j.Id == sourceId, cancellationToken),
            "KbActSection" => callerPermissions.Contains("kb.read.all") && await db.KbActSections.IgnoreQueryFilters().AnyAsync(s => s.TenantId == tenantId && s.Id == sourceId, cancellationToken),
            _ => false,
        };

    /// <summary>Reuses IDocumentService.GetByIdAsync's own Privileged-confidentiality visibility check directly — not a re-implementation. Additionally enforces Module 16 Security: "Privileged-marked docs excluded from AI unless matter owner enables per-matter 'AI allowed' flag."</summary>
    private async Task<bool> CanAccessDocumentAsync(Guid tenantId, Guid documentId, IReadOnlyCollection<string> callerPermissions, CancellationToken cancellationToken)
    {
        var document = await documentService.GetByIdAsync(tenantId, documentId, callerPermissions, cancellationToken);
        if (document is null)
        {
            return false;
        }

        if (document.Confidentiality != "Privileged")
        {
            return true;
        }

        if (!document.MatterId.HasValue)
        {
            return false;
        }

        var aiAllowed = await db.Matters
            .Where(m => m.TenantId == tenantId && m.Id == document.MatterId.Value)
            .Select(m => (bool?)m.AiAllowed)
            .SingleOrDefaultAsync(cancellationToken);

        return aiAllowed == true;
    }

    /// <summary>No per-record read check exists on IMatterService.GetByIdAsync today (confirmed — it takes no caller/permission parameter); this resolves the caller's own matters.read.{own|team|branch|all} grant via the same scope-rank logic PermissionEvaluator uses to gate the controller endpoint, applied as a row-level check. "team" scope means matter-team membership (legal.matter_team_members), the natural per-matter reading of "team" rather than the org-wide core.teams grouping. Also enforces Matter.AiAllowed (Module 16 Security).</summary>
    private async Task<bool> CanAccessMatterAsync(Guid tenantId, Guid callerId, Guid matterId, CancellationToken cancellationToken)
    {
        var matter = await db.Matters.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Id == matterId, cancellationToken);
        if (matter is null || !matter.AiAllowed)
        {
            return false;
        }

        var permissions = await permissionService.GetEffectivePermissionsAsync(callerId, tenantId, cancellationToken);
        var bestScope = permissions
            .Where(p => string.Equals(p.Module, "matters", StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.Action, "read", StringComparison.OrdinalIgnoreCase)
                && ScopeRank.ContainsKey(p.Scope))
            .OrderByDescending(p => ScopeRank[p.Scope])
            .Select(p => p.Scope)
            .FirstOrDefault();

        if (bestScope is null)
        {
            return false;
        }

        switch (bestScope.ToLowerInvariant())
        {
            case "all":
                return true;
            case "branch":
                var callerBranchId = await db.Users.Where(u => u.TenantId == tenantId && u.Id == callerId).Select(u => u.BranchId).SingleOrDefaultAsync(cancellationToken);
                return matter.BranchId.HasValue && matter.BranchId == callerBranchId;
            case "team":
                return matter.ResponsibleLawyerId == callerId
                    || await db.MatterTeamMembers.AnyAsync(m => m.TenantId == tenantId && m.MatterId == matterId && m.UserId == callerId && !m.IsDeleted, cancellationToken);
            default: // own
                return matter.ResponsibleLawyerId == callerId;
        }
    }
}
