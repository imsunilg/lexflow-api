using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to fin.invoices (lexflow-database Scripts/06_Fin/Invoices).
/// Module 8. BR-4 invoice immutability is enforced first at the DB (004_Triggers.sql blocks
/// UPDATE of content fields once status != Draft) and again here defensively: every content
/// mutator below throws once <see cref="Status"/> leaves Draft, so the application layer never
/// even attempts the doomed UPDATE — the DB trigger remains the ultimate backstop for any path
/// that bypasses this entity (defense in depth, per this module's own build brief).
/// </summary>
public sealed class Invoice : AuditableEntity
{
    private Invoice()
    {
    }

    public Invoice(Guid tenantId, Guid matterId, Guid clientId, DateOnly? issueDate, DateOnly? dueDate, string currency, string? notes)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        MatterId = matterId;
        ClientId = clientId;
        Number = string.Empty;
        Status = "Draft";
        IssueDate = issueDate;
        DueDate = dueDate;
        Currency = currency;
        Notes = notes;
    }

    public string Number { get; private set; } = null!;
    public Guid? SeriesId { get; private set; }
    public Guid MatterId { get; private set; }
    public Guid ClientId { get; private set; }
    public string Status { get; private set; } = "Draft";
    public DateOnly? IssueDate { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public string Currency { get; private set; } = "INR";
    public decimal SubTotal { get; private set; }
    public decimal DiscountTotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal GrandTotal { get; private set; }
    public decimal AmountPaid { get; private set; }
    public string? Notes { get; private set; }
    public string? PdfBlobPath { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? VoidedAt { get; private set; }
    public string? VoidReason { get; private set; }

    private void EnsureDraft()
    {
        if (Status != "Draft")
        {
            throw new InvalidOperationException($"BR-4 violated: invoice {Id} is not Draft (status {Status}) — lines/totals are immutable once sent; correct via credit note instead.");
        }
    }

    public void SetTotals(decimal subTotal, decimal discountTotal, decimal taxTotal, decimal grandTotal)
    {
        EnsureDraft();
        SubTotal = subTotal;
        DiscountTotal = discountTotal;
        TaxTotal = taxTotal;
        GrandTotal = grandTotal;
    }

    public void UpdateDraft(DateOnly? issueDate, DateOnly? dueDate, string? notes)
    {
        EnsureDraft();
        IssueDate = issueDate;
        DueDate = dueDate;
        Notes = notes;
    }

    public void AssignNumber(Guid seriesId, string number)
    {
        EnsureDraft();
        SeriesId = seriesId;
        Number = number;
    }

    public void Submit() => Status = TransitionFrom("Draft", "Submitted");

    public void Approve() => Status = TransitionFrom("Submitted", "Approved");

    public void Reject() => Status = TransitionFrom("Submitted", "Rejected");

    public void BackToDraft() => Status = TransitionFrom("Rejected", "Draft");

    public void SetPdf(string pdfBlobPath) => PdfBlobPath = pdfBlobPath;

    public void MarkSent()
    {
        if (Status is not ("Approved" or "Draft"))
        {
            throw new InvalidOperationException($"Invoice {Id} cannot be sent from status {Status}.");
        }

        Status = "Sent";
        SentAt = DateTimeOffset.UtcNow;
    }

    public void ApplyPayment(decimal amount)
    {
        AmountPaid += amount;
        Status = AmountPaid >= GrandTotal ? "Paid" : "PartiallyPaid";
    }

    public void UnapplyPayment(decimal amount)
    {
        AmountPaid = Math.Max(0, AmountPaid - amount);
        Status = AmountPaid <= 0 ? "Sent" : "PartiallyPaid";
    }

    public void MarkOverdue()
    {
        if (Status is "Sent" or "PartiallyPaid")
        {
            Status = "Overdue";
        }
    }

    /// <summary>BR-4/Module 8 Validation Rules: "void only if unpaid (else credit note path)". Voided numbers are never reused (AC-B5) — the row stays, only status/void_reason/voided_at change.</summary>
    public void Void(string reason)
    {
        if (AmountPaid > 0)
        {
            throw new InvalidOperationException("An invoice with payments applied cannot be voided; issue a credit note instead.");
        }

        Status = "Void";
        VoidReason = reason;
        VoidedAt = DateTimeOffset.UtcNow;
    }

    private string TransitionFrom(string expected, string next)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Invoice {Id} cannot transition to {next} from status {Status} (expected {expected}).");
        }

        return next;
    }
}
