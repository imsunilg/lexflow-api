namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// AC-U1: "deactivated user's active JWTs rejected ≤ 60 s (denylist check)". Access
/// tokens are stateless JWTs valid up to 15 minutes (PRD §20(3)) — without this,
/// a token issued moments before deactivation would keep working until it expires.
/// Checked on every request in JwtBearerEvents.OnTokenValidated (Api/Program.cs).
/// </summary>
public interface IUserDenylistService
{
    Task DenylistAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> IsDenylistedAsync(Guid userId, CancellationToken cancellationToken = default);
}
