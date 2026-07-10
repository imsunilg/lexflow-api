using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.matter_expenses (lexflow-database Scripts/04_Legal/MatterExpenses).</summary>
public sealed class MatterExpense : AuditableEntity
{
    private MatterExpense()
    {
    }

    public MatterExpense(Guid tenantId, Guid matterId, DateOnly incurredOn, string? category, string? description, decimal amount, bool billable, Guid? receiptDocumentId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        MatterId = matterId;
        IncurredOn = incurredOn;
        Category = category;
        Description = description;
        Amount = amount;
        Billable = billable;
        ReceiptDocumentId = receiptDocumentId;
    }

    public Guid MatterId { get; private set; }
    public DateOnly IncurredOn { get; private set; }
    public string? Category { get; private set; }
    public string? Description { get; private set; }
    public decimal Amount { get; private set; }
    public bool Billable { get; private set; }
    public Guid? ReceiptDocumentId { get; private set; }
}
