using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to portal.portal_appointment_requests (lexflow-database Scripts/19_Portal/PortalAppointmentRequests). Module 17 User Flow #6.</summary>
public sealed class PortalAppointmentRequest : AuditableEntity
{
    private PortalAppointmentRequest()
    {
    }

    public PortalAppointmentRequest(Guid tenantId, Guid clientPortalUserId, Guid matterId, Guid lawyerId, DateTimeOffset requestedStart, DateTimeOffset requestedEnd, string? notes)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ClientPortalUserId = clientPortalUserId;
        MatterId = matterId;
        LawyerId = lawyerId;
        RequestedStart = requestedStart;
        RequestedEnd = requestedEnd;
        Notes = notes;
        Status = "Requested";
    }

    public Guid ClientPortalUserId { get; private set; }
    public Guid MatterId { get; private set; }
    public Guid LawyerId { get; private set; }
    public DateTimeOffset RequestedStart { get; private set; }
    public DateTimeOffset RequestedEnd { get; private set; }
    public string? Notes { get; private set; }
    public string Status { get; private set; } = "Requested";
    public DateTimeOffset? ConfirmedStart { get; private set; }
    public DateTimeOffset? ConfirmedEnd { get; private set; }
    public Guid? CalendarEventId { get; private set; }

    public void Confirm(DateTimeOffset start, DateTimeOffset end, Guid? calendarEventId)
    {
        Status = "Confirmed";
        ConfirmedStart = start;
        ConfirmedEnd = end;
        CalendarEventId = calendarEventId;
    }

    public void Reschedule(DateTimeOffset start, DateTimeOffset end)
    {
        Status = "Rescheduled";
        ConfirmedStart = start;
        ConfirmedEnd = end;
    }

    public void Decline() => Status = "Declined";

    public void Cancel() => Status = "Cancelled";
}
