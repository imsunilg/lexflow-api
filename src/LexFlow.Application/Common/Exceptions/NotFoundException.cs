namespace LexFlow.Application.Common.Exceptions;

/// <summary>Maps to HTTP 404 NOT_FOUND. Also used for cross-tenant access to prevent enumeration (PRD §28).</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"Entity \"{entityName}\" ({key}) was not found.")
    {
    }
}
