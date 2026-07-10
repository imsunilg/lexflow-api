using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ops.recurrence_exceptions (lexflow-database Scripts/07_Ops/RecurrenceExceptions).
/// AC-CAL3: editing/deleting "this occurrence only" of a recurring series creates one of
/// these rather than mutating the series master event.
/// </summary>
public sealed class RecurrenceException : AuditableEntity
{
    private RecurrenceException()
    {
    }

    public RecurrenceException(Guid tenantId, Guid eventId, DateOnly occurrenceDate, string exceptionType, DateTimeOffset? overrideStartsAt, DateTimeOffset? overrideEndsAt)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EventId = eventId;
        OccurrenceDate = occurrenceDate;
        ExceptionType = exceptionType;
        OverrideStartsAt = overrideStartsAt;
        OverrideEndsAt = overrideEndsAt;
    }

    public Guid EventId { get; private set; }
    public DateOnly OccurrenceDate { get; private set; }
    public string ExceptionType { get; private set; } = null!;
    public DateTimeOffset? OverrideStartsAt { get; private set; }
    public DateTimeOffset? OverrideEndsAt { get; private set; }
}
