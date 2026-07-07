namespace LexFlow.Api.Contracts;

/// <summary>Error envelope shape, PRD §17: <c>{ success: false, error: { code, message, traceId, details } }</c>.</summary>
public sealed class ApiErrorResponse
{
    public bool Success { get; } = false;
    public required ApiError Error { get; init; }
}

public sealed class ApiError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public required string TraceId { get; init; }
    public IReadOnlyCollection<ApiErrorDetail>? Details { get; init; }
}

public sealed class ApiErrorDetail
{
    public required string Field { get; init; }
    public required string Code { get; init; }
}
