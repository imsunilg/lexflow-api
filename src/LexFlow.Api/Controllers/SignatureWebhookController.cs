using LexFlow.Application.Commands.Documents;
using LexFlow.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>
/// POST /api/v1/webhooks/signature/{provider}/{tenantId} (PRD §17/§34 — provider-called,
/// unauthenticated). The tenant id rides in the URL path rather than the payload: DocuSign
/// Connect and Adobe Sign webhooks are configured with a per-account callback URL, so
/// embedding the tenant id there (same idea as the web-to-lead capture endpoint's formKey)
/// is how this build resolves RLS's app.tenant_id before touching dms.signature_envelopes,
/// since there's no JWT on this path to source a tenant claim from otherwise. AC-DOC6.
/// </summary>
[ApiController]
[Route("api/v1/webhooks/signature")]
[AllowAnonymous]
public sealed class SignatureWebhookController(IMediator mediator, LexFlowDbContext dbContext) : ControllerBase
{
    [HttpPost("{provider}/{tenantId:guid}")]
    public async Task<IActionResult> Handle(string provider, Guid tenantId, CancellationToken cancellationToken)
    {
        await dbContext.SetTenantIdAsync(tenantId, cancellationToken);

        using var reader = new StreamReader(Request.Body);
        var rawPayload = await reader.ReadToEndAsync(cancellationToken);
        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());

        await mediator.Send(new HandleSignatureWebhookCommand(tenantId, provider, rawPayload, headers), cancellationToken);
        return Ok();
    }
}
