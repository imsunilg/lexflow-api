using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ops.event_reminders (lexflow-database Scripts/07_Ops/EventReminders).
/// Polymorphic (<see cref="EventRefKind"/> selects which entity <see cref="EventRefId"/> points
/// at — "hearing" for this build), same pattern as dms.document_permissions.principal_id.
/// </summary>
public sealed class EventReminder : AuditableEntity
{
    private EventReminder()
    {
    }

    public EventReminder(Guid tenantId, string eventRefKind, Guid eventRefId, int offsetMinutes, string channel)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EventRefKind = eventRefKind;
        EventRefId = eventRefId;
        OffsetMinutes = offsetMinutes;
        Channel = channel;
        Status = "Pending";
    }

    public string EventRefKind { get; private set; } = null!;
    public Guid EventRefId { get; private set; }
    public int OffsetMinutes { get; private set; }
    public string Channel { get; private set; } = null!;
    public string Status { get; private set; } = "Pending";

    public void MarkSent() => Status = "Sent";

    public void MarkFailed() => Status = "Failed";
}
