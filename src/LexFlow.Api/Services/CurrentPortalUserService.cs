using LexFlow.Application.Common.Interfaces;

namespace LexFlow.Api.Services;

/// <summary>
/// Module 17 Security: "client_id from token, never from payload." Reads exclusively off
/// the validated "Portal"-scheme JWT's claims — sub (portal user id), tenant, client — never
/// off route/query/body. Same claim-reading style as CurrentUserService (staff), deliberately
/// a separate class rather than a shared base, per Module 17's "complete identity separation."
/// </summary>
public sealed class CurrentPortalUserService(IHttpContextAccessor httpContextAccessor) : ICurrentPortalUserService
{
    public Guid? PortalUserId => TryGetGuidClaim("sub");

    public Guid? TenantId => TryGetGuidClaim("tenant");

    public Guid? ClientId => TryGetGuidClaim("client");

    private Guid? TryGetGuidClaim(string claimType)
    {
        var value = httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
        return Guid.TryParse(value, out var guid) ? guid : null;
    }
}
