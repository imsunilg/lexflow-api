using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to fin.time_entries (lexflow-database Scripts/06_Fin/TimeEntries).
/// Module 9. BR-5 (enforced first at the DB via 004_Triggers.sql, and again here defensively —
/// every mutator below throws once <see cref="Status"/> is Billed, so the application layer
/// never even attempts the doomed UPDATE): entries become immutable once Billed.
/// </summary>
public sealed class TimeEntry : AuditableEntity
{
    private TimeEntry()
    {
    }

    public TimeEntry(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        Guid? activityCodeId,
        DateOnly entryDate,
        DateTimeOffset? startedAt,
        int durationMin,
        int roundedMin,
        bool billable,
        string? narrative,
        string? internalNote,
        string source)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        UserId = userId;
        MatterId = matterId;
        ActivityCodeId = activityCodeId;
        EntryDate = entryDate;
        StartedAt = startedAt;
        DurationMin = durationMin;
        RoundedMin = roundedMin;
        Billable = billable;
        Narrative = narrative;
        InternalNote = internalNote;
        Source = source;
        Status = "Draft";
    }

    public Guid UserId { get; private set; }
    public Guid MatterId { get; private set; }
    public Guid? ActivityCodeId { get; private set; }
    public DateOnly EntryDate { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public int DurationMin { get; private set; }
    public int RoundedMin { get; private set; }
    public bool Billable { get; private set; }
    public string? Narrative { get; private set; }
    public string? InternalNote { get; private set; }
    public string Status { get; private set; } = "Draft";
    public decimal? RateSnapshot { get; private set; }
    public decimal? AmountSnapshot { get; private set; }
    public Guid? InvoiceLineId { get; private set; }
    public string Source { get; private set; } = "manual";
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }

    private void EnsureNotBilled()
    {
        if (Status == "Billed")
        {
            throw new InvalidOperationException("BR-5 violated: time entry is Billed and immutable; correct via credit note + rebill.");
        }
    }

    public void UpdateDraft(Guid matterId, Guid? activityCodeId, DateOnly entryDate, int durationMin, int roundedMin, bool billable, string? narrative, string? internalNote)
    {
        EnsureNotBilled();
        MatterId = matterId;
        ActivityCodeId = activityCodeId;
        EntryDate = entryDate;
        DurationMin = durationMin;
        RoundedMin = roundedMin;
        Billable = billable;
        Narrative = narrative;
        InternalNote = internalNote;

        // A previously Rejected entry that's edited and resubmitted keeps its own row (history
        // chain via core.audit_events on this row per §30), moving back to Draft.
        if (Status == "Rejected")
        {
            Status = "Draft";
        }
    }

    public void Submit()
    {
        EnsureNotBilled();
        Status = "Submitted";
    }

    /// <summary>BR-7: rate snapshot is resolved and frozen at approval time — later rate-card changes never retroactively affect an approved entry.</summary>
    public void Approve(Guid approvedBy, decimal rateSnapshot, decimal amountSnapshot)
    {
        EnsureNotBilled();
        Status = "Approved";
        ApprovedBy = approvedBy;
        ApprovedAt = DateTimeOffset.UtcNow;
        RateSnapshot = rateSnapshot;
        AmountSnapshot = amountSnapshot;
    }

    public void Reject()
    {
        EnsureNotBilled();
        Status = "Rejected";
    }

    /// <summary>Links an Approved entry to the Draft invoice line pulling it as WIP, without yet transitioning to Billed — the entry stays editable/write-down-able while the invoice itself is still Draft (Module 8 User Flow #3). See MarkBilled for the final, immutable transition at Send.</summary>
    public void LinkToDraftInvoiceLine(Guid invoiceLineId)
    {
        EnsureNotBilled();
        InvoiceLineId = invoiceLineId;
    }

    public void MarkBilled(Guid invoiceLineId)
    {
        EnsureNotBilled();
        Status = "Billed";
        InvoiceLineId = invoiceLineId;
    }

    /// <summary>Module 8 Edge Cases: a Draft invoice line pulling this entry was removed/edited before send — release it back to unbilled WIP.</summary>
    public void UnlinkFromInvoiceLine()
    {
        EnsureNotBilled();
        InvoiceLineId = null;
    }

    public void WriteOff()
    {
        EnsureNotBilled();
        Status = "WrittenOff";
    }
}
