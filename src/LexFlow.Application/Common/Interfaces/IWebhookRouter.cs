namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// §34: "webhook ingress: single /api/v1/webhooks/{provider} router -> signature
/// verification -> dedupe (event id store 7 d) -> outbox event." One entry point for
/// every inbound provider callback in this build (SMS, WhatsApp, Voice status, email
/// bounce/inbound); the tenant id rides in the URL path (same precedented adaptation as
/// SignatureWebhookController/DocumentShareLinkService/the calendar sync webhook — none
/// of those providers' payloads carry a LexFlow tenant id, and RLS needs app.tenant_id
/// set before any query can even run).
/// </summary>
public interface IWebhookRouter
{
    Task<WebhookHandleResult> HandleAsync(Guid tenantId, string provider, string rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);
}

public sealed record WebhookHandleResult(bool SignatureValid, bool Duplicate, bool Handled, string? Message);
