namespace LexFlow.Domain.Common;

/// <summary>Marker for domain events raised by aggregates and dispatched after SaveChanges.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
