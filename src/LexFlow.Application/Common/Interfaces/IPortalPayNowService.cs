namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 17 "Pay Now" (PRD Pay-Now sequence diagram): POST /invoices/{id}/pay -> gateway
/// checkout session; return-URL reconciliation is race-safe against a same-day bank payment
/// per the documented edge case ("gateway session voided when invoice fully allocated; race ->
/// auto-refund flow + alert").
/// </summary>
public interface IPortalPayNowService
{
    /// <summary>Throws NotFoundException if invoiceId does not belong to clientId; throws DomainRuleException("INVOICE_ALREADY_PAID", ...) if the invoice has no open balance.</summary>
    Task<PortalPaySessionDto> CreateSessionAsync(Guid tenantId, Guid clientId, Guid? clientPortalUserId, Guid invoiceId, string returnUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// The return-URL handler. Deliberately does not require a live portal session/token —
    /// PRD edge case: "expired session mid-payment -> gateway return URL revalidates and
    /// completes reconciliation server-side regardless." Scoped instead by the unguessable
    /// UUID sessionId plus tenantId embedded in the return URL itself. Idempotent: safe to
    /// call multiple times (e.g. a slow network causing a retry) and safe to run concurrently
    /// with a webhook-driven capture of the same session.
    /// </summary>
    Task<PortalPayReconcileResultDto> ReconcileReturnAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken = default);
}

public sealed record PortalPaySessionDto(Guid SessionId, string CheckoutUrl, string Gateway, decimal Amount);

public sealed record PortalPayReconcileResultDto(string Status, bool InvoicePaid, string? Message);
