namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ai.ai_tenant_quotas (lexflow-database Scripts/18_AI/AiTenantQuotas).
/// Module 16 Architecture: "per-tenant monthly AI-credit quota by tier." Single-column PK
/// (tenant_id) — same shape as fin.running_timers' user_id PK, see that entity's own doc comment.
/// Usage for the current month is derived from ai.ai_interactions.credits_charged, not stored
/// here — this row only holds the configured ceiling.
/// </summary>
public sealed class AiTenantQuota
{
    private AiTenantQuota()
    {
    }

    public AiTenantQuota(Guid tenantId, decimal monthlyCreditLimit)
    {
        TenantId = tenantId;
        MonthlyCreditLimit = monthlyCreditLimit;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public decimal MonthlyCreditLimit { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void SetLimit(decimal monthlyCreditLimit)
    {
        MonthlyCreditLimit = monthlyCreditLimit;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
