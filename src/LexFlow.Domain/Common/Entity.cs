namespace LexFlow.Domain.Common;

/// <summary>
/// Base type for every aggregate/entity. Mirrors the PK convention in PRD §14:
/// all primary keys are <c>uuid DEFAULT gen_random_uuid()</c> in the database.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }
}
