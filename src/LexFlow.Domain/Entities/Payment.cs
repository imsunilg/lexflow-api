using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to fin.payments (lexflow-database Scripts/06_Fin/Payments). Module 8. idempotency_key UNIQUE(tenant_id, idempotency_key) at the DB backs AC-B3's "duplicate webhook makes no double entry".</summary>
public sealed class Payment : AuditableEntity
{
    private Payment()
    {
    }

    public Payment(Guid tenantId, string receiptNumber, Guid clientId, decimal amount, string mode, string? gateway, string? gatewayRef, DateOnly receivedOn, string? idempotencyKey)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ReceiptNumber = receiptNumber;
        ClientId = clientId;
        Amount = amount;
        Mode = mode;
        Gateway = gateway;
        GatewayRef = gatewayRef;
        ReceivedOn = receivedOn;
        Status = "Pending";
        IdempotencyKey = idempotencyKey;
    }

    public string ReceiptNumber { get; private set; } = null!;
    public Guid ClientId { get; private set; }
    public decimal Amount { get; private set; }
    public string Mode { get; private set; } = null!;
    public string? Gateway { get; private set; }
    public string? GatewayRef { get; private set; }
    public DateOnly ReceivedOn { get; private set; }
    public string Status { get; private set; } = "Pending";
    public string? IdempotencyKey { get; private set; }

    public void MarkCleared() => Status = "Cleared";

    /// <summary>Edge case: "trust deposit cheque bounces (reversing entry + alert + invoice payment auto-unapplied)" — the same Bounced transition applies to a regular (non-trust) payment.</summary>
    public void MarkBounced() => Status = "Bounced";

    public void MarkRefunded() => Status = "Refunded";

    public void MarkVoided() => Status = "Voided";
}
