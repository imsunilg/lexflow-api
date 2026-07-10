using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to portal.payment_gateway_sessions (lexflow-database
/// Scripts/19_Portal/PaymentGatewaySessions). Module 17 "Pay Now" — tracks a client-facing
/// checkout attempt so the return-URL handler can reconcile idempotently and race-safely
/// against fin.payments (the system of record for money actually received). Not itself an
/// accounting record.
/// </summary>
public sealed class PaymentGatewaySession
{
    private PaymentGatewaySession()
    {
    }

    public PaymentGatewaySession(Guid tenantId, Guid invoiceId, Guid? clientPortalUserId, string gateway, decimal amount, string? returnUrl)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        InvoiceId = invoiceId;
        ClientPortalUserId = clientPortalUserId;
        Gateway = gateway;
        Amount = amount;
        ReturnUrl = returnUrl;
        Status = "Created";
        IdempotencyKey = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid? ClientPortalUserId { get; private set; }
    public string Gateway { get; private set; } = null!;
    public string? GatewayRef { get; private set; }
    public decimal Amount { get; private set; }
    public string Status { get; private set; } = "Created";
    public Guid IdempotencyKey { get; private set; }
    public string? ReturnUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public void MarkPending(string gatewayRef)
    {
        GatewayRef = gatewayRef;
        Status = "Pending";
    }

    public void MarkCaptured()
    {
        Status = "Captured";
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Double-payment guard: the invoice was already fully allocated (e.g. a same-day bank payment recorded first) by the time this session's payment was captured.</summary>
    public void MarkVoided()
    {
        Status = "Voided";
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkRefunded() => Status = "Refunded";
}
