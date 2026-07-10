using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to crm.lead_stage_history (lexflow-database Scripts/03_CRM/LeadStageHistory). Module 2.</summary>
public sealed class LeadStageHistory : AuditableEntity
{
    private LeadStageHistory()
    {
    }

    public LeadStageHistory(Guid tenantId, Guid leadId, string? fromStage, string toStage, Guid? byUser)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        LeadId = leadId;
        FromStage = fromStage;
        ToStage = toStage;
        At = DateTimeOffset.UtcNow;
        ByUser = byUser;
    }

    public Guid LeadId { get; private set; }
    public string? FromStage { get; private set; }
    public string ToStage { get; private set; } = null!;
    public DateTimeOffset At { get; private set; }
    public Guid? ByUser { get; private set; }
}
