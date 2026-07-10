namespace LexFlow.Infrastructure.Ops;

/// <summary>
/// Keyless projection of ops.v_calendar_items (a DB view unioning ops.calendar_events,
/// legal.hearings, legal.matter_important_dates, ops.tasks — lexflow-database
/// Scripts/11_Views/001_v_calendar_items.sql). Postgres-only (EF InMemory has no concept
/// of a view); CalendarService falls back to querying the four source tables directly
/// under InMemory, guarded the same way as every other relational-only query in this
/// codebase (see IsRelational() usage elsewhere).
/// </summary>
public sealed class CalendarItemRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ItemKind { get; set; } = null!;
    public string Title { get; set; } = null!;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public bool AllDay { get; set; }
    public Guid? MatterId { get; set; }
    public string? Location { get; set; }
    public string? Status { get; set; }
    public bool IsLocked { get; set; }
}
