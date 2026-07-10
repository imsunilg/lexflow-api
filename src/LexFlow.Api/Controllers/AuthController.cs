using System.Diagnostics;
using LexFlow.Api.Contracts;
using LexFlow.Api.Contracts.Auth;
using LexFlow.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Implements the auth endpoints from PRD §17: login/refresh/logout/forgot/reset/2fa/me.</summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService, ICurrentUserService currentUser) : ControllerBase
{
    private const string RefreshCookieName = "lexflow_refresh";
    private const string TwoFaPendingCookieName = "lexflow_2fa_pending";

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await authService.LoginAsync(request.TenantSlug, request.Email, request.Password, ip, userAgent, cancellationToken);

        return result.Outcome switch
        {
            LoginOutcome.Succeeded => LoginSucceededResponse(result),
            LoginOutcome.RequiresTwoFactor => TwoFaRequiredResponse(result.PendingToken!),
            LoginOutcome.AccountLocked => Error(423, "ACCOUNT_LOCKED", "Account is temporarily locked due to repeated failed login attempts."),
            _ => Error(401, "INVALID_CREDENTIALS", "Email or password is incorrect."),
        };
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) || string.IsNullOrEmpty(refreshToken))
        {
            return Error(401, "UNAUTHENTICATED", "No refresh token present.");
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        try
        {
            var result = await authService.RefreshAsync(refreshToken, ip, userAgent, cancellationToken);
            SetRefreshCookie(result.RefreshToken);
            return Ok(ApiResponse<RefreshResponse>.Of(new RefreshResponse(result.AccessToken, result.ExpiresIn)));
        }
        catch (UnauthorizedAccessException)
        {
            Response.Cookies.Delete(RefreshCookieName);
            return Error(401, "UNAUTHENTICATED", "Refresh token is invalid, expired, or already used.");
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) && !string.IsNullOrEmpty(refreshToken))
        {
            await authService.LogoutAsync(refreshToken, cancellationToken);
        }

        Response.Cookies.Delete(RefreshCookieName);
        return NoContent();
    }

    [HttpPost("forgot")]
    [AllowAnonymous]
    public async Task<IActionResult> Forgot([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await authService.RequestPasswordResetAsync(request.TenantSlug, request.Email, cancellationToken);
        return Ok(ApiResponse<object>.Of(new { }));
    }

    [HttpPost("reset")]
    [AllowAnonymous]
    public async Task<IActionResult> Reset([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await authService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);
            return Ok(ApiResponse<object>.Of(new { }));
        }
        catch (UnauthorizedAccessException)
        {
            return Error(401, "UNAUTHENTICATED", "Reset token is invalid or expired.");
        }
    }

    [HttpPost("2fa/setup")]
    [Authorize]
    public async Task<IActionResult> TwoFaSetup(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentUser.TenantId is not { } tenantId)
        {
            return Error(401, "UNAUTHENTICATED", "No authenticated user.");
        }

        var result = await authService.SetupTwoFactorAsync(userId, tenantId, cancellationToken);
        return Ok(ApiResponse<TwoFaSetupResponse>.Of(new TwoFaSetupResponse(result.ProvisioningUri, result.RecoveryCodes)));
    }

    [HttpPost("2fa/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> TwoFaVerify([FromBody] TwoFaVerifyRequest request, CancellationToken cancellationToken)
    {
        // Two distinct callers share this endpoint (PRD §17 shows only `{ code }` in the body):
        // (a) completing a login that returned 428 TWO_FA_REQUIRED — the pending token travels
        //     as the short-lived cookie set by /login, not as a request field;
        // (b) an already-authenticated user confirming 2FA enrollment started via /2fa/setup.
        if (Request.Cookies.TryGetValue(TwoFaPendingCookieName, out var pendingToken) && !string.IsNullOrEmpty(pendingToken))
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();

            var result = await authService.CompleteTwoFactorLoginAsync(pendingToken, request.Code, ip, userAgent, cancellationToken);
            Response.Cookies.Delete(TwoFaPendingCookieName);

            return result.Outcome == LoginOutcome.Succeeded
                ? LoginSucceededResponse(result)
                : Error(401, "INVALID_CREDENTIALS", "Invalid or expired 2FA code.");
        }

        if (currentUser.UserId is { } userId && currentUser.TenantId is { } tenantId)
        {
            try
            {
                await authService.ConfirmTwoFactorAsync(userId, tenantId, request.Code, cancellationToken);
                return Ok(ApiResponse<object>.Of(new { }));
            }
            catch (UnauthorizedAccessException)
            {
                return Error(401, "INVALID_CREDENTIALS", "Invalid 2FA code.");
            }
        }

        return Error(401, "UNAUTHENTICATED", "No pending 2FA challenge or authenticated session.");
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentUser.TenantId is not { } tenantId)
        {
            return Error(401, "UNAUTHENTICATED", "No authenticated user.");
        }

        var info = await authService.GetMeAsync(userId, tenantId, cancellationToken);
        return Ok(ApiResponse<MeResponse>.Of(new MeResponse(info.Id, info.Name, info.Email, info.Role, info.BranchId, info.Permissions, info.TwoFaEnabled)));
    }

    private IActionResult LoginSucceededResponse(LexFlow.Application.Common.Interfaces.LoginResult result)
    {
        SetRefreshCookie(result.RefreshToken!);
        var user = result.User is null
            ? null
            : new LoginUserDto(result.User.Id, result.User.Name, result.User.Role, result.User.Permissions, result.User.BranchId);

        return Ok(ApiResponse<LoginResponse>.Of(new LoginResponse(result.AccessToken!, result.ExpiresIn!.Value, false, user)));
    }

    private IActionResult TwoFaRequiredResponse(string pendingToken)
    {
        Response.Cookies.Append(TwoFaPendingCookieName, pendingToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromMinutes(5),
        });

        var response = new LoginResponse(string.Empty, 0, true, null);
        return StatusCode(428, ApiResponse<LoginResponse>.Of(response));
    }

    private void SetRefreshCookie(string refreshToken)
    {
        Response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(7),
        });
    }

    private IActionResult Error(int statusCode, string code, string message)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var response = new ApiErrorResponse
        {
            Error = new ApiError { Code = code, Message = message, TraceId = traceId },
        };

        return StatusCode(statusCode, response);
    }
}
