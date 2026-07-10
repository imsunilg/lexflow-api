namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 16 Security: "RBAC-filtered retrieval (test-enforced)" — "retrieval must call the same
/// permission-check used by the record's normal read endpoint, no separate, weaker path" (build
/// brief). For Document and KbArticle, whose normal read endpoints already resolve a real
/// per-record visibility rule (IDocumentService.GetByIdAsync's Privileged-confidentiality gate;
/// IKbArticleService.GetAsync's author/reviewer/published gate), this calls that exact method —
/// not a re-implementation of its logic. Matter, KbJudgment, KbActSection, and KbCollection have
/// no per-record check on their normal read endpoints today either (confirmed: IMatterService.
/// GetByIdAsync and the KB Act/Judgment services take no caller/permission parameter at all) —
/// for those, this resolves the caller's own module.read.{own|team|branch|all} grant via the
/// same PermissionEvaluator scope-rank logic PermissionHandler uses to gate the controller
/// action, and applies it as a row-level predicate, which is the closest honest equivalent of
/// "the same check" when no finer-grained one exists yet. AC-AI1's red-team test exercises this
/// directly: a user without access to a document must never see it retrieved.
/// </summary>
public interface IAiRetrievalGuard
{
    Task<bool> CanAccessAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, string sourceKind, Guid sourceId, CancellationToken cancellationToken = default);
}
