namespace LexFlow.Application.Common.Exceptions;

/// <summary>Maps to HTTP 429 RATE_LIMITED (PRD FR-016, §28). RetryAfterSeconds becomes the Retry-After header.</summary>
public sealed class RateLimitExceededException : Exception
{
    public int RetryAfterSeconds { get; }

    public RateLimitExceededException(int retryAfterSeconds)
        : base("Too many requests.")
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
