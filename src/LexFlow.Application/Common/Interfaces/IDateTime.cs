namespace LexFlow.Application.Common.Interfaces;

/// <summary>Testable clock abstraction — handlers never call DateTimeOffset.UtcNow directly.</summary>
public interface IDateTime
{
    DateTimeOffset UtcNow { get; }
}
