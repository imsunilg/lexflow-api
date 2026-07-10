using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to fin.invoice_lines (lexflow-database Scripts/06_Fin/InvoiceLines). Module 8.</summary>
public sealed class InvoiceLine : AuditableEntity
{
    private InvoiceLine()
    {
    }

    public InvoiceLine(Guid tenantId, Guid invoiceId, int lineNo, string type, string? description, decimal qty, string? unit, decimal rate, decimal amount, Guid[]? timeEntryIds, Guid[]? expenseIds)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        InvoiceId = invoiceId;
        LineNo = lineNo;
        Type = type;
        Description = description;
        Qty = qty;
        Unit = unit;
        Rate = rate;
        Amount = amount;
        TimeEntryIds = timeEntryIds ?? [];
        ExpenseIds = expenseIds ?? [];
    }

    public Guid InvoiceId { get; private set; }
    public int LineNo { get; private set; }
    public string Type { get; private set; } = null!;
    public string? Description { get; private set; }
    public decimal Qty { get; private set; }
    public string? Unit { get; private set; }
    public decimal Rate { get; private set; }
    public decimal Amount { get; private set; }
    public Guid[] TimeEntryIds { get; private set; } = [];
    public Guid[] ExpenseIds { get; private set; } = [];
}
