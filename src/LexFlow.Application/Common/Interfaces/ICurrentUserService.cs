namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Request-scoped accessor for the authenticated principal, sourced from JWT claims
/// per PRD §20(3): sub, tenant, role, branch. Infrastructure/Api implement this from
/// HttpContext; Application/Domain never touch HttpContext directly.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    Guid? BranchId { get; }
    IReadOnlyCollection<string> Permissions { get; }
}
