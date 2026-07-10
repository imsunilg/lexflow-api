namespace LexFlow.Application.Common.Exceptions;

/// <summary>Module 16 Error Handling: "quota exceeded -&gt; 402-style AI_QUOTA_EXCEEDED with upgrade CTA."</summary>
public sealed class AiQuotaExceededException(decimal limit, decimal used) : Exception($"Monthly AI-credit quota exceeded ({used:0.##}/{limit:0.##} credits used).")
{
    public decimal Limit { get; } = limit;
    public decimal Used { get; } = used;
}
