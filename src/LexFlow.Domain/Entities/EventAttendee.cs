using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to ops.event_attendees (lexflow-database Scripts/07_Ops/EventAttendees).</summary>
public sealed class EventAttendee : AuditableEntity
{
    private EventAttendee()
    {
    }

    public EventAttendee(Guid tenantId, Guid eventId, Guid? userId, string? email, string? name, bool isOrganizer)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EventId = eventId;
        UserId = userId;
        Email = email;
        Name = name;
        IsOrganizer = isOrganizer;
        Status = "Pending";
    }

    public Guid EventId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Email { get; private set; }
    public string? Name { get; private set; }
    public bool IsOrganizer { get; private set; }
    public string Status { get; private set; } = "Pending";

    public void SetStatus(string status) => Status = status;
}
