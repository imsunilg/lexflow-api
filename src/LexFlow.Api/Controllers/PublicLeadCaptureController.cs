using LexFlow.Application.Commands.Leads;
using LexFlow.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LexFlow.Api.Controllers;

/// <summary>
/// POST /api/v1/public/web-to-lead/{formKey} (Module 2: "web-to-lead API/embed form" —
/// unauthenticated, captcha + rate-limited). formKey is the tenant's public embed-form key
/// (its tenant id in "N" format, per-tenant embed forms carry no other config in this build —
/// there's no forms/captcha-provider table yet, so this is the honest minimal version: honeypot
/// + a fixed-window rate limit stand in for the captcha this PRD line calls for). Writes only —
/// returns no data, even on a honeypot trip (silently absorbed rather than surfaced).
/// </summary>
[ApiController]
[Route("api/v1/public/web-to-lead")]
[AllowAnonymous]
[EnableRateLimiting("web-to-lead")]
public sealed class PublicLeadCaptureController(IMediator mediator, LexFlowDbContext dbContext) : ControllerBase
{
    [HttpPost("{formKey}")]
    public async Task<IActionResult> Capture(string formKey, [FromBody] WebToLeadRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(formKey, out var tenantId))
        {
            return NoContent();
        }

        // No authenticated principal on this path, so TenantScopingMiddleware never runs —
        // set the RLS session variable directly here instead (PRD §14/§20(5)).
        await dbContext.SetTenantIdAsync(tenantId, cancellationToken);

        await mediator.Send(new CaptureWebToLeadCommand(tenantId, request.FirstName, request.LastName, request.Email, request.PhoneE164, request.IssueSummary, request.Website), cancellationToken);
        return NoContent();
    }
}

/// <summary><c>Website</c> is the honeypot field — a real visitor never fills it (hidden via CSS on the embed form).</summary>
public sealed record WebToLeadRequest(string FirstName, string? LastName, string? Email, string? PhoneE164, string? IssueSummary, string? Website);
