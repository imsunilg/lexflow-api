using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ops.calendar_events (lexflow-database Scripts/07_Ops/CalendarEvents).
/// Native events only — hearings/tasks/deadlines are projected in via ops.v_calendar_items
/// (queried directly by CalendarService, not modeled as an entity here since it's a DB view).
/// </summary>
public sealed class CalendarEvent : AuditableEntity
{
    private CalendarEvent()
    {
    }

    public CalendarEvent(Guid tenantId, string kind, string title, DateTimeOffset startsAt, DateTimeOffset endsAt, bool allDay, string? location, string? videoLink, Guid? matterId, string? rrule, Guid? seriesId, Guid? organizerId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Kind = kind;
        Title = title;
        StartsAt = startsAt;
        EndsAt = endsAt;
        AllDay = allDay;
        Location = location;
        VideoLink = videoLink;
        MatterId = matterId;
        Rrule = rrule;
        SeriesId = seriesId;
        OrganizerId = organizerId;
    }

    public string Kind { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public bool AllDay { get; private set; }
    public string? Location { get; private set; }
    public string? VideoLink { get; private set; }
    public Guid? MatterId { get; private set; }
    public string? Rrule { get; private set; }
    public Guid? SeriesId { get; private set; }
    public Guid? OrganizerId { get; private set; }

    public void Update(string title, DateTimeOffset startsAt, DateTimeOffset endsAt, bool allDay, string? location, string? videoLink, string? rrule)
    {
        Title = title;
        StartsAt = startsAt;
        EndsAt = endsAt;
        AllDay = allDay;
        Location = location;
        VideoLink = videoLink;
        Rrule = rrule;
    }
}
