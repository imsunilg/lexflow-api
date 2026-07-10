using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ops.workflow_runs (lexflow-database Scripts/07_Ops/WorkflowRuns).
/// §23: "each run logged (workflow_runs) with input snapshot + action results; retries 3x."
/// </summary>
public sealed class WorkflowRun : AuditableEntity
{
    private WorkflowRun()
    {
    }

    public WorkflowRun(Guid tenantId, Guid ruleId, string? triggerRef)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        RuleId = ruleId;
        TriggerRef = triggerRef;
        Status = "Pending";
    }

    public Guid RuleId { get; private set; }
    public string? TriggerRef { get; private set; }
    public string Status { get; private set; } = "Pending";
    public DateTimeOffset? ExecutedAt { get; private set; }
    public string ResultJson { get; private set; } = "{}";

    public void MarkSucceeded(string resultJson)
    {
        Status = "Succeeded";
        ExecutedAt = DateTimeOffset.UtcNow;
        ResultJson = resultJson;
    }

    public void MarkFailed(string resultJson)
    {
        Status = "Failed";
        ExecutedAt = DateTimeOffset.UtcNow;
        ResultJson = resultJson;
    }
}
