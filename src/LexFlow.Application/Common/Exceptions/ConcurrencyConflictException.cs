namespace LexFlow.Application.Common.Exceptions;

/// <summary>
/// Maps to HTTP 412 PRECONDITION_FAILED (PRD §28: "concurrency (ETag/If-Match on PUT
/// of versioned aggregates)"; FR-006: optimistic concurrency via xmin/rowversion).
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The record was modified by another user. Reload and retry.")
    {
    }
}
