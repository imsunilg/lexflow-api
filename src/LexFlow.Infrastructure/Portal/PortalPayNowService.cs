using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexFlow.Infrastructure.Portal;

/// <summary>
/// Module 17 "Pay Now" (PRD Pay-Now sequence diagram). Session creation picks the first
/// tenant-configured gateway that successfully creates a checkout (same "per firm config"
/// resolution every other gateway-touching feature in this codebase relies on via
/// GatewayCredentialResolver, here delegated to each IPaymentGateway implementation itself).
///
/// Race-safety (documented edge case: "invoice paid at bank same day as gateway
/// (double-payment guard: gateway session voided when invoice fully allocated; race ->
/// auto-refund flow + alert)"): ReconcileReturnAsync re-checks the invoice's open balance at
/// capture time, not just at session-creation time. If the invoice was already fully settled
/// by another route (e.g. a same-day bank payment recorded by staff) before this gateway
/// capture is reconciled, the captured money is still recorded (a Payment row — it was
/// genuinely received) but with zero allocation, then immediately refunded via
/// IPaymentService.CreateRefundAsync, and a Critical-level alert is raised the same way
/// AuditIntegrityCheckJob raises "page-worthy" violations (LogCritical + audit.audit_events
/// row) rather than silently leaving an unallocated stray payment.
/// </summary>
public sealed class PortalPayNowService(
    LexFlowDbContext db,
    IEnumerable<IPaymentGateway> gateways,
    IPaymentService paymentService,
    ILogger<PortalPayNowService> logger) : IPortalPayNowService
{
    public async Task<PortalPaySessionDto> CreateSessionAsync(Guid tenantId, Guid clientId, Guid? clientPortalUserId, Guid invoiceId, string returnUrl, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.SingleOrDefaultAsync(i => i.TenantId == tenantId && i.Id == invoiceId, cancellationToken);
        if (invoice is null || invoice.ClientId != clientId || invoice.Status == "Draft")
        {
            throw new NotFoundException(nameof(Invoice), invoiceId);
        }

        var openBalance = invoice.GrandTotal - invoice.AmountPaid;
        if (openBalance <= 0)
        {
            throw new DomainRuleException("INVOICE_ALREADY_PAID", $"Invoice {invoiceId} has no open balance.");
        }

        // Only one active checkout session per invoice at a time — voiding any prior
        // Created/Pending session before opening a new one is itself part of the race-safety
        // story (an abandoned earlier session can never be captured after the fact and
        // double-count against this invoice).
        var priorSessions = await db.PaymentGatewaySessions
            .Where(s => s.TenantId == tenantId && s.InvoiceId == invoiceId && (s.Status == "Created" || s.Status == "Pending"))
            .ToListAsync(cancellationToken);
        foreach (var prior in priorSessions)
        {
            prior.MarkVoided();
        }

        foreach (var gateway in gateways)
        {
            var result = await gateway.CreatePaymentLinkAsync(tenantId, invoiceId, openBalance, invoice.Currency, $"Invoice {invoice.Number}", cancellationToken);
            if (result.Success && result.Url is not null && result.GatewayRef is not null)
            {
                var session = new PaymentGatewaySession(tenantId, invoiceId, clientPortalUserId, gateway.Provider, openBalance, returnUrl);
                session.MarkPending(result.GatewayRef);
                await db.PaymentGatewaySessions.AddAsync(session, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                return new PortalPaySessionDto(session.Id, result.Url, gateway.Provider, openBalance);
            }
        }

        throw new DomainRuleException("NO_PAYMENT_GATEWAY_CONFIGURED", "No payment gateway is configured for this tenant.");
    }

    public async Task<PortalPayReconcileResultDto> ReconcileReturnAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await db.PaymentGatewaySessions.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Id == sessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaymentGatewaySession), sessionId);

        if (session.Status is "Captured" or "Voided" or "Refunded")
        {
            // Idempotent replay: a webhook may have already reconciled this session before the
            // client's browser reached the return URL, or vice versa — return the settled state
            // rather than re-processing.
            var settledInvoice = await db.Invoices.SingleAsync(i => i.Id == session.InvoiceId, cancellationToken);
            return new PortalPayReconcileResultDto(session.Status, settledInvoice.AmountPaid >= settledInvoice.GrandTotal, null);
        }

        if (session.GatewayRef is null)
        {
            return new PortalPayReconcileResultDto(session.Status, false, "Checkout was never completed with the gateway.");
        }

        var gateway = gateways.SingleOrDefault(g => g.Provider == session.Gateway);
        if (gateway is null)
        {
            return new PortalPayReconcileResultDto(session.Status, false, "Gateway is no longer configured.");
        }

        var status = await gateway.GetPaymentStatusAsync(tenantId, session.GatewayRef, cancellationToken);
        if (!status.Captured)
        {
            return new PortalPayReconcileResultDto(session.Status, false, status.Error);
        }

        var capturedAmount = status.CapturedAmount ?? session.Amount;
        var invoice = await db.Invoices.SingleAsync(i => i.Id == session.InvoiceId, cancellationToken);
        var openBalance = invoice.GrandTotal - invoice.AmountPaid;

        if (openBalance <= 0)
        {
            await ApplyDoublePaymentGuardAsync(session, invoice, capturedAmount, cancellationToken);
            return new PortalPayReconcileResultDto("Voided", true, "Invoice was already fully paid; captured amount has been queued for refund.");
        }

        await paymentService.HandleGatewayCapturedAsync(tenantId, session.Gateway, session.GatewayRef, capturedAmount, session.InvoiceId, cancellationToken);
        session.MarkCaptured();
        await db.SaveChangesAsync(cancellationToken);

        var reloadedInvoice = await db.Invoices.SingleAsync(i => i.Id == session.InvoiceId, cancellationToken);
        return new PortalPayReconcileResultDto("Captured", reloadedInvoice.AmountPaid >= reloadedInvoice.GrandTotal, null);
    }

    private async Task ApplyDoublePaymentGuardAsync(PaymentGatewaySession session, Invoice invoice, decimal capturedAmount, CancellationToken cancellationToken)
    {
        var payment = await paymentService.RecordPaymentAsync(session.TenantId, new RecordPaymentInput(
            invoice.ClientId, capturedAmount, "Gateway", session.Gateway, session.GatewayRef, DateOnly.FromDateTime(DateTime.UtcNow), [], $"paynow-race-{session.Id}"), cancellationToken);

        await paymentService.CreateRefundAsync(session.TenantId, payment.Id, capturedAmount,
            "Auto-refund: invoice was already fully allocated before this gateway capture was reconciled (Module 17 double-payment guard).", cancellationToken);

        session.MarkVoided();
        await db.SaveChangesAsync(cancellationToken);

        logger.LogCritical(
            "Pay-Now double-payment race detected for tenant {TenantId}, invoice {InvoiceId}, session {SessionId}: gateway captured {Amount} after the invoice was already fully allocated. Auto-refund queued.",
            session.TenantId, session.InvoiceId, session.Id, capturedAmount);

        await db.AuditEvents.AddAsync(new AuditEvent(
            session.TenantId, actorUserId: null, actorType: "System", action: "paynow.double_payment_race",
            entityType: "Invoice", entityId: session.InvoiceId,
            before: null, after: $"{{\"sessionId\":\"{session.Id}\",\"capturedAmount\":{capturedAmount}}}",
            ip: null, ua: null, traceId: null), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
