using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to fin.refunds (lexflow-database Scripts/06_Fin/Refunds). Module 8: "refunds (gateway-linked)".</summary>
public sealed class Refund : AuditableEntity
{
    private Refund()
    {
    }

    public Refund(Guid tenantId, Guid paymentId, decimal amount, string? reason)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        PaymentId = paymentId;
        Amount = amount;
        Reason = reason;
        Status = "Requested";
    }

    public Guid PaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public string? Reason { get; private set; }
    public string Status { get; private set; } = "Requested";
    public string? GatewayRef { get; private set; }
    public DateOnly? RefundedOn { get; private set; }

    public void MarkProcessed(string? gatewayRef)
    {
        Status = "Processed";
        GatewayRef = gatewayRef;
        RefundedOn = DateOnly.FromDateTime(DateTime.UtcNow);
    }

    public void MarkFailed() => Status = "Failed";
}
