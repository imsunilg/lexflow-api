namespace LexFlow.Api.Contracts;

/// <summary>Success envelope shape, PRD §17: <c>{ success, data, meta }</c>.</summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; } = true;
    public T? Data { get; init; }
    public ApiResponseMeta? Meta { get; init; }

    public static ApiResponse<T> Of(T data, ApiResponseMeta? meta = null) => new() { Data = data, Meta = meta };
}

public sealed class ApiResponseMeta
{
    public string? Cursor { get; init; }
    public bool? HasMore { get; init; }
}
