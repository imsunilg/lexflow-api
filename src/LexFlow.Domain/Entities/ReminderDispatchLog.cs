using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ops.reminder_dispatch_log (lexflow-database
/// Scripts/07_Ops/ReminderDispatchLog — PARTITION BY RANGE(sent_at), PK (id, sent_at)).
/// AC-CAL2: "dispatch log proves it" — one row per channel actually attempted for a given
/// ops.event_reminders row.
/// </summary>
public sealed class ReminderDispatchLog : AuditableEntity
{
    private ReminderDispatchLog()
    {
    }

    public ReminderDispatchLog(Guid tenantId, Guid reminderId, string channel, string status, string? providerRef, string? error)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ReminderId = reminderId;
        Channel = channel;
        SentAt = DateTimeOffset.UtcNow;
        Status = status;
        ProviderRef = providerRef;
        Error = error;
    }

    public Guid ReminderId { get; private set; }
    public string Channel { get; private set; } = null!;
    public DateTimeOffset SentAt { get; private set; }
    public string Status { get; private set; } = "Sent";
    public string? ProviderRef { get; private set; }
    public string? Error { get; private set; }
}
