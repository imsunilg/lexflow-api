namespace LexFlow.Api.Contracts.Portal;

/// <summary>Request/response DTOs for /api/portal/v1/*, shaped per PRD §17/Module 17.</summary>
public sealed record PortalLoginRequest(string Email, string Password, string TenantSlug);

public sealed record PortalLoginUserDto(Guid Id, Guid ClientId, string? Name, string Email);

public sealed record PortalLoginResponse(string AccessToken, int ExpiresIn, PortalLoginUserDto? User);

public sealed record PortalRefreshResponse(string AccessToken, int ExpiresIn);

public sealed record PortalForgotPasswordRequest(string TenantSlug, string Email);

public sealed record PortalResetPasswordRequest(string Token, string NewPassword);

public sealed record PortalPayNowRequest(string ReturnUrl);

public sealed record PortalUploadDocumentRequest(Guid MatterId, string Title);

public sealed record PortalAppointmentRequestDto(Guid MatterId, Guid LawyerId, DateTimeOffset RequestedStart, DateTimeOffset RequestedEnd, string? Notes);

public sealed record PortalPostMessageRequest(string Body);
