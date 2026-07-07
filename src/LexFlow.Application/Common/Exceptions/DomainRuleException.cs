namespace LexFlow.Application.Common.Exceptions;

/// <summary>
/// Maps to HTTP 422 UNPROCESSABLE (PRD §17/§28). <see cref="SubCode"/> carries one of the
/// documented sub-codes, e.g. CONFLICT_OF_INTEREST_SUSPECTED, INSUFFICIENT_TRUST_BALANCE,
/// TAX_NOT_CONFIGURED, TIMERS_RUNNING, CYCLE_DETECTED, MALWARE_DETECTED, AI_QUOTA_EXCEEDED.
/// </summary>
public sealed class DomainRuleException : Exception
{
    public string SubCode { get; }
    public object? Details { get; }

    public DomainRuleException(string subCode, string message, object? details = null)
        : base(message)
    {
        SubCode = subCode;
        Details = details;
    }
}
