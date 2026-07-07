using LexFlow.Application.Common.Interfaces;

namespace LexFlow.Api.Services;

/// <summary>
/// Reads the authenticated principal's claims per PRD §20(3): sub, tenant, role, branch.
/// Full permission set is fetched server-side and cached in Redis (60 s) — wired once
/// auth (Prompt C-1) lands; for now this reads whatever claims are present on the principal.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private System.Security.Claims.ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId => TryGetGuidClaim("sub");

    public Guid? TenantId => TryGetGuidClaim("tenant");

    public Guid? BranchId => TryGetGuidClaim("branch");

    public IReadOnlyCollection<string> Permissions =>
        User?.Claims.Where(c => c.Type == "perm").Select(c => c.Value).ToArray() ?? [];

    private Guid? TryGetGuidClaim(string claimType)
    {
        var value = User?.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
        return Guid.TryParse(value, out var guid) ? guid : null;
    }
}
