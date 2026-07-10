namespace LexFlow.Api.Contracts.Auth;

/// <summary>Request/response DTOs for the auth endpoints, shaped exactly per PRD §17.</summary>
public sealed record LoginRequest(string Email, string Password, string TenantSlug);

public sealed record LoginUserDto(Guid Id, string Name, string Role, IReadOnlyCollection<string> Permissions, Guid? BranchId);

public sealed record LoginResponse(string AccessToken, int ExpiresIn, bool Requires2fa, LoginUserDto? User);

public sealed record TwoFaVerifyRequest(string Code);

public sealed record RefreshResponse(string AccessToken, int ExpiresIn);

public sealed record ForgotPasswordRequest(string TenantSlug, string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record TwoFaSetupResponse(string ProvisioningUri, IReadOnlyList<string> RecoveryCodes);

public sealed record MeResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    Guid? BranchId,
    IReadOnlyCollection<string> Permissions,
    bool TwoFaEnabled);
