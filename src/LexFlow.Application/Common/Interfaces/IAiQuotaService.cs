namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 16 Architecture: "Cost: per-tenant monthly AI-credit quota by tier; metered per feature; graceful 'quota reached' state."</summary>
public interface IAiQuotaService
{
    /// <summary>Throws AiQuotaExceededException when charging <paramref name="estimatedCredits"/> would exceed the tenant's remaining monthly quota. Called before the LLM call so a request that would blow the budget never spends provider tokens.</summary>
    Task EnsureWithinQuotaAsync(Guid tenantId, decimal estimatedCredits, CancellationToken cancellationToken = default);

    Task<AiQuotaStatus> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed record AiQuotaStatus(decimal MonthlyLimit, decimal UsedThisMonth, decimal Remaining);
