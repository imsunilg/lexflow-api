namespace LexFlow.Application.Common.Exceptions;

/// <summary>Maps to HTTP 403 FORBIDDEN — record exists but the caller's permission scope denies it (PRD §20, §28).</summary>
public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException() : base("Access to this resource is forbidden.")
    {
    }
}
