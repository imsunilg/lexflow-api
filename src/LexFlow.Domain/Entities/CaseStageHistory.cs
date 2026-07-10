using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.case_stage_history (lexflow-database Scripts/04_Legal/CaseStageHistory, additive migration).</summary>
public sealed class CaseStageHistory : AuditableEntity
{
    private CaseStageHistory()
    {
    }

    public CaseStageHistory(Guid tenantId, Guid caseId, string? fromStage, string toStage, Guid? byUser)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CaseId = caseId;
        FromStage = fromStage;
        ToStage = toStage;
        At = DateTimeOffset.UtcNow;
        ByUser = byUser;
    }

    public Guid CaseId { get; private set; }
    public string? FromStage { get; private set; }
    public string ToStage { get; private set; } = null!;
    public DateTimeOffset At { get; private set; }
    public Guid? ByUser { get; private set; }
}
