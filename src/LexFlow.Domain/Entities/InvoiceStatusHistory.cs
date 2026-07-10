using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to fin.invoice_status_history (lexflow-database Scripts/06_Fin/InvoiceStatusHistory). AC-B5: "audit trail complete Draft->Void."</summary>
public sealed class InvoiceStatusHistory : AuditableEntity
{
    private InvoiceStatusHistory()
    {
    }

    public InvoiceStatusHistory(Guid tenantId, Guid invoiceId, string? fromStatus, string toStatus, string? reason, Guid? changedBy)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        InvoiceId = invoiceId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = reason;
        ChangedBy = changedBy;
        ChangedAt = DateTimeOffset.UtcNow;
    }

    public Guid InvoiceId { get; private set; }
    public string? FromStatus { get; private set; }
    public string ToStatus { get; private set; } = null!;
    public string? Reason { get; private set; }
    public Guid? ChangedBy { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
}
