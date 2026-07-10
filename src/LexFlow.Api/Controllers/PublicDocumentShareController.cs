using LexFlow.Application.Commands.Documents;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>
/// GET /api/v1/public/shared/{token} (public, guarded). AC-DOC5: expired link returns a
/// branded 410 page; every access attempt is logged regardless of outcome. The tenant
/// context for this unauthenticated path is resolved inside
/// IDocumentShareLinkService.AccessAsync from the token itself (see that service's own
/// doc comment on the token format) — there is no JWT here to source it from.
/// </summary>
[ApiController]
[Route("api/v1/public/shared")]
[AllowAnonymous]
public sealed class PublicDocumentShareController(IMediator mediator) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<IActionResult> Access(string token, [FromQuery] string? password, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await mediator.Send(new AccessShareLinkCommand(token, password, ip, userAgent), cancellationToken);
        if (!result.Success)
        {
            // AC-DOC5: "branded 410 page" for an expired/exhausted/missing link — the SPA
            // renders the actual branded page, the API just needs the right status/reason.
            // A wrong/missing password is the one failure that isn't "gone", so it gets 401.
            var statusCode = result.FailureReason == "PASSWORD_REQUIRED_OR_INVALID" ? StatusCodes.Status401Unauthorized : StatusCodes.Status410Gone;
            return StatusCode(statusCode, new { error = result.FailureReason });
        }

        return Redirect(result.DownloadUrl!);
    }
}
