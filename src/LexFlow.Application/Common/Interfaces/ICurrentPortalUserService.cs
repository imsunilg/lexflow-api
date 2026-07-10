namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 17 Security: "object-level checks on every request (client_id from token, never
/// from payload)." Resolves the portal caller's identity exclusively from the validated
/// "Portal"-scheme JWT's claims (sub, tenant, client) — never from route/query/body — so every
/// portal handler's ownership scope is structurally impossible to spoof via the request payload.
/// </summary>
public interface ICurrentPortalUserService
{
    Guid? PortalUserId { get; }
    Guid? TenantId { get; }
    Guid? ClientId { get; }
}
