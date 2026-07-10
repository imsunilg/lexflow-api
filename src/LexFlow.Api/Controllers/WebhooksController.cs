using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>
/// §34: "webhook ingress: single /api/v1/webhooks/{provider} router -> signature
/// verification -> dedupe (event id store 7 d) -> outbox event." Provider-called,
/// unauthenticated (no JWT), so the tenant id rides in the URL path — same precedented
/// adaptation as SignatureWebhookController/DocumentShareLinkService/the calendar sync
/// webhook (RLS needs app.tenant_id set before any query can run, and none of these
/// providers' payloads carry a LexFlow tenant id). Every inbound comm-channel callback
/// in this build (Twilio SMS/Voice status, WhatsApp Cloud API, MSG91, an inbound-email
/// webhook variant of the BCC-dropbox pipeline) funnels through this one action.
/// </summary>
[ApiController]
[Route("api/v1/webhooks")]
[AllowAnonymous]
public sealed class WebhooksController(IWebhookRouter webhookRouter, LexFlowDbContext dbContext) : ControllerBase
{
    [HttpPost("{provider}/{tenantId:guid}")]
    public async Task<IActionResult> Handle(string provider, Guid tenantId, CancellationToken cancellationToken)
    {
        await dbContext.SetTenantIdAsync(tenantId, cancellationToken);

        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
        headers["X-Webhook-Url"] = $"{Request.Scheme}://{Request.Host}{Request.Path}";

        var result = await webhookRouter.HandleAsync(tenantId, provider, rawBody, headers, cancellationToken);

        if (!result.SignatureValid)
        {
            return Unauthorized();
        }

        return Ok();
    }
}
