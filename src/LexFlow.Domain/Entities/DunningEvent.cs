using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to fin.dunning_events (lexflow-database Scripts/06_Fin/DunningEvents). Module 8: reminder schedule occurrences, per-invoice mute, escalation at +30.</summary>
public sealed class DunningEvent : AuditableEntity
{
    private DunningEvent()
    {
    }

    public DunningEvent(Guid tenantId, Guid invoiceId, Guid? scheduleId, string stepLabel, string channel, DateTimeOffset scheduledFor)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        InvoiceId = invoiceId;
        ScheduleId = scheduleId;
        StepLabel = stepLabel;
        Channel = channel;
        ScheduledFor = scheduledFor;
        Status = "Pending";
        Muted = false;
    }

    public Guid InvoiceId { get; private set; }
    public Guid? ScheduleId { get; private set; }
    public string StepLabel { get; private set; } = null!;
    public string Channel { get; private set; } = null!;
    public DateTimeOffset ScheduledFor { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string Status { get; private set; } = "Pending";
    public bool Muted { get; private set; }

    public void Mute() => Muted = true;

    public void MarkSent()
    {
        Status = "Sent";
        SentAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed() => Status = "Failed";

    public void MarkSkipped() => Status = "Skipped";
}
