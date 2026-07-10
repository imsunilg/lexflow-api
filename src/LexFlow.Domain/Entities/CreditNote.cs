using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to fin.credit_notes (lexflow-database Scripts/06_Fin/CreditNotes). BR-4: corrections to sent/immutable invoices go via credit note + reissue.</summary>
public sealed class CreditNote : AuditableEntity
{
    private CreditNote()
    {
    }

    public CreditNote(Guid tenantId, string number, Guid invoiceId, decimal amount, string reason)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Number = number;
        InvoiceId = invoiceId;
        Amount = amount;
        Reason = reason;
        Status = "Draft";
    }

    public string Number { get; private set; } = null!;
    public Guid InvoiceId { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = null!;
    public string Status { get; private set; } = "Draft";

    public void Issue() => Status = "Issued";

    public void Apply() => Status = "Applied";

    public void Void() => Status = "Void";
}
