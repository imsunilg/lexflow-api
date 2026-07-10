namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Resolves a portal caller's ClientPortalUser.VisibleMatterIds (Module 17: "corporate:
/// several logins per client, each with matter-visibility subset"). Not carried as a JWT
/// claim (the array is unbounded in size and can change between token refreshes), so every
/// portal controller action re-resolves it from the DB per request via this — consistent
/// with the codebase-wide "sensitive checks always DB-verified" posture (PRD §20.4).
/// </summary>
public interface IPortalScopeService
{
    /// <summary>Null means "all matters visible" (individual client default); non-null restricts to the given subset.</summary>
    Task<Guid[]?> GetVisibleMatterIdsAsync(Guid tenantId, Guid portalUserId, CancellationToken cancellationToken = default);
}
