namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 13 Security: "Report data respects row-level scope of runner (a team-scope partner's
/// revenue report contains only their team) — enforced by injecting scope predicates into every
/// report query." Resolves the caller's highest granted scope for the "reports.operational" or
/// "reports.financial" permission area (PRD §21 rows "Reports operational"/"Reports financial")
/// into a concrete, queryable predicate: which lawyer_key values and/or which branch the runner
/// is confined to. This is new infrastructure — unlike <c>PermissionEvaluator</c>, which only
/// gates whether an endpoint may be called at all, this determines which *rows* come back.
/// </summary>
public interface IReportScopeService
{
    Task<ReportScope> ResolveAsync(Guid tenantId, Guid userId, string permissionArea, CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="Kind"/> is one of own/team/branch/all/none (PRD §21 scope ranks). For own/team,
/// <see cref="LawyerKeys"/> lists the exact set of user ids the runner may see facts for (self,
/// or self + every fellow member across every team the runner belongs to). For branch,
/// <see cref="BranchId"/> narrows facts to the runner's home branch. For all/none no/all rows
/// (respectively) are permitted — none means the caller lacks the permission area entirely.
/// </summary>
public sealed record ReportScope(string Kind, IReadOnlyCollection<Guid>? LawyerKeys, Guid? BranchId)
{
    public static ReportScope None() => new("none", [], null);

    public bool IsDenied => Kind == "none";
}
