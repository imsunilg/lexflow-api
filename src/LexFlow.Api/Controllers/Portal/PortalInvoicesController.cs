using LexFlow.Api.Contracts;
using LexFlow.Api.Contracts.Portal;
using LexFlow.Api.Security;
using LexFlow.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers.Portal;

/// <summary>Module 17 User Flow #4 + "Pay Now": invoices list/detail, gateway checkout-session creation, and return-URL reconciliation.</summary>
[ApiController]
[Route("api/portal/v1")]
[EnableCors("LexFlowPortal")]
public sealed class PortalInvoicesController(IPortalInvoiceService invoiceService, IPortalPayNowService payNowService, ICurrentPortalUserService currentPortalUser) : ControllerBase
{
    [HttpGet("invoices")]
    [Authorize(AuthenticationSchemes = PortalAuthenticationDefaults.AuthenticationScheme)]
    public async Task<IActionResult> GetInvoices(CancellationToken cancellationToken)
    {
        var (_, clientId) = RequireIdentity();
        var invoices = await invoiceService.GetInvoicesAsync(RequireTenantId(), clientId, cancellationToken);
        return Ok(ApiResponse<object>.Of(invoices));
    }

    [HttpGet("invoices/{invoiceId:guid}")]
    [Authorize(AuthenticationSchemes = PortalAuthenticationDefaults.AuthenticationScheme)]
    public async Task<IActionResult> GetInvoice(Guid invoiceId, CancellationToken cancellationToken)
    {
        var (_, clientId) = RequireIdentity();
        var invoice = await invoiceService.GetInvoiceAsync(RequireTenantId(), clientId, invoiceId, cancellationToken);
        return Ok(ApiResponse<object>.Of(invoice));
    }

    [HttpPost("invoices/{invoiceId:guid}/pay")]
    [Authorize(AuthenticationSchemes = PortalAuthenticationDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Pay(Guid invoiceId, [FromBody] PortalPayNowRequest request, CancellationToken cancellationToken)
    {
        var (portalUserId, clientId) = RequireIdentity();
        var session = await payNowService.CreateSessionAsync(RequireTenantId(), clientId, portalUserId, invoiceId, request.ReturnUrl, cancellationToken);
        return Ok(ApiResponse<object>.Of(session));
    }

    /// <summary>
    /// The gateway return URL. Deliberately AllowAnonymous — PRD edge case: "expired session
    /// mid-payment -> gateway return URL revalidates and completes reconciliation
    /// server-side regardless." Scoped by the unguessable UUID sessionId (plus tenantId in
    /// the route) rather than a live portal session; PortalPayNowService.ReconcileReturnAsync
    /// re-derives everything else (invoice, gateway, amount) from that session row itself,
    /// never from a query parameter.
    /// </summary>
    [HttpGet("invoices/pay/return/{tenantId:guid}/{sessionId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> PayReturn(Guid tenantId, Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await payNowService.ReconcileReturnAsync(tenantId, sessionId, cancellationToken);
        return Ok(ApiResponse<object>.Of(result));
    }

    private Guid RequireTenantId() => currentPortalUser.TenantId ?? throw new UnauthorizedAccessException("No authenticated portal user.");

    private (Guid PortalUserId, Guid ClientId) RequireIdentity()
    {
        if (currentPortalUser.PortalUserId is not { } portalUserId || currentPortalUser.ClientId is not { } clientId)
        {
            throw new UnauthorizedAccessException("No authenticated portal user.");
        }

        return (portalUserId, clientId);
    }
}
