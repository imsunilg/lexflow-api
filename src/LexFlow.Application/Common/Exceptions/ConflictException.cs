namespace LexFlow.Application.Common.Exceptions;

/// <summary>Maps to HTTP 409 CONFLICT (PRD §28: "state/uniqueness/idempotent-replay").</summary>
public sealed class ConflictException : Exception
{
    public string Code { get; }

    public ConflictException(string message, string code = "CONFLICT")
        : base(message)
    {
        Code = code;
    }
}
