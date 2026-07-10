using System.Diagnostics;
using LexFlow.Api.Contracts;
using LexFlow.Api.Contracts.Portal;
using LexFlow.Api.Security;
using LexFlow.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace LexFlow.Api.Controllers.Portal;

/// <summary>
/// Module 17: POST /auth/login|refresh|forgot(+reset). Separate JWT audience ("portal",
/// via the "Portal" auth scheme) and a separate refresh cookie — "lexflow_portal_refresh",
/// never "lexflow_refresh" — optionally scoped to a distinct cookie domain via
/// Portal:CookieDomain config, so it coexists with (and is never confused with) the staff
/// cookie even if both apps happen to share a parent domain.
/// </summary>
[ApiController]
[Route("api/portal/v1/auth")]
[EnableCors("LexFlowPortal")]
public sealed class PortalAuthController(IPortalAuthService portalAuthService, IConfiguration configuration) : ControllerBase
{
    private const string RefreshCookieName = "lexflow_portal_refresh";

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] PortalLoginRequest request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await portalAuthService.LoginAsync(request.TenantSlug, request.Email, request.Password, ip, userAgent, cancellationToken);

        return result.Outcome switch
        {
            PortalLoginOutcome.Succeeded => LoginSucceededResponse(result),
            PortalLoginOutcome.AccountLocked => Error(423, "ACCOUNT_LOCKED", "Account is temporarily locked due to repeated failed login attempts."),
            PortalLoginOutcome.PortalDisabled => Error(403, "PORTAL_DISABLED", "Portal access has been disabled for this account."),
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
            var result = await portalAuthService.RefreshAsync(refreshToken, ip, userAgent, cancellationToken);
            SetRefreshCookie(result.RefreshToken);
            return Ok(ApiResponse<PortalRefreshResponse>.Of(new PortalRefreshResponse(result.AccessToken, result.ExpiresIn)));
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
            await portalAuthService.LogoutAsync(refreshToken, cancellationToken);
        }

        Response.Cookies.Delete(RefreshCookieName);
        return NoContent();
    }

    [HttpPost("forgot")]
    [AllowAnonymous]
    public async Task<IActionResult> Forgot([FromBody] PortalForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await portalAuthService.RequestPasswordResetAsync(request.TenantSlug, request.Email, cancellationToken);
        return Ok(ApiResponse<object>.Of(new { }));
    }

    /// <summary>Also the invite-acceptance endpoint — the same purpose token is issued whether the caller's ClientPortalUser.Status is Invited or Active.</summary>
    [HttpPost("reset")]
    [AllowAnonymous]
    public async Task<IActionResult> Reset([FromBody] PortalResetPasswordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await portalAuthService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);
            return Ok(ApiResponse<object>.Of(new { }));
        }
        catch (UnauthorizedAccessException)
        {
            return Error(401, "UNAUTHENTICATED", "Reset token is invalid or expired.");
        }
    }

    private IActionResult LoginSucceededResponse(PortalLoginResult result)
    {
        SetRefreshCookie(result.RefreshToken!);
        var user = result.User is null ? null : new PortalLoginUserDto(result.User.Id, result.User.ClientId, result.User.Name, result.User.Email);
        return Ok(ApiResponse<PortalLoginResponse>.Of(new PortalLoginResponse(result.AccessToken!, result.ExpiresIn!.Value, user)));
    }

    private void SetRefreshCookie(string refreshToken)
    {
        var cookieDomain = configuration["Portal:CookieDomain"];
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(7),
        };

        if (!string.IsNullOrWhiteSpace(cookieDomain))
        {
            options.Domain = cookieDomain;
        }

        Response.Cookies.Append(RefreshCookieName, refreshToken, options);
    }

    private IActionResult Error(int statusCode, string code, string message)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return StatusCode(statusCode, new ApiErrorResponse { Error = new ApiError { Code = code, Message = message, TraceId = traceId } });
    }
}
