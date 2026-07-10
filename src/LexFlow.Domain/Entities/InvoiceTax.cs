using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to fin.invoice_taxes (lexflow-database Scripts/06_Fin/InvoiceTaxes). Module 8 / BR-9: CGST+SGST (intra-state) vs IGST (inter-state) vs zero-rated (SEZ/export).</summary>
public sealed class InvoiceTax : AuditableEntity
{
    private InvoiceTax()
    {
    }

    public InvoiceTax(Guid tenantId, Guid invoiceId, string name, decimal ratePct, decimal taxableAmount, decimal amount)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        InvoiceId = invoiceId;
        Name = name;
        RatePct = ratePct;
        TaxableAmount = taxableAmount;
        Amount = amount;
    }

    public Guid InvoiceId { get; private set; }
    public string Name { get; private set; } = null!;
    public decimal RatePct { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal Amount { get; private set; }
}
