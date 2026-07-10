using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to fin.payment_allocations (lexflow-database Scripts/06_Fin/PaymentAllocations). §19.8: "over-allocation impossible (Σallocations ≤ payment.amount and per-invoice Σ ≤ open balance)".</summary>
public sealed class PaymentAllocation : AuditableEntity
{
    private PaymentAllocation()
    {
    }

    public PaymentAllocation(Guid tenantId, Guid paymentId, Guid invoiceId, decimal amount)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        PaymentId = paymentId;
        InvoiceId = invoiceId;
        Amount = amount;
    }

    public Guid PaymentId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public decimal Amount { get; private set; }
}
