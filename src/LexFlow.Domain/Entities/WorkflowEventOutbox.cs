using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ops.workflow_event_outbox (lexflow-database
/// Scripts/07_Ops/WorkflowEventOutbox, an additive migration for this build). Written in
/// the same transaction as the domain mutation that raised the event (e.g. lead.created,
/// hearing.outcome_recorded); WorkflowEventDispatchService polls Pending rows and runs
/// matching ops.workflow_rules against <see cref="Payload"/> — same shape as
/// DocumentIndexOutbox in the DMS module.
/// </summary>
public sealed class WorkflowEventOutbox : AuditableEntity
{
    private WorkflowEventOutbox()
    {
    }

    public WorkflowEventOutbox(Guid tenantId, string eventType, Guid? entityId, string payload)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EventType = eventType;
        EntityId = entityId;
        Payload = payload;
        Status = "Pending";
    }

    public string EventType { get; private set; } = null!;
    public Guid? EntityId { get; private set; }
    public string Payload { get; private set; } = "{}";
    public string Status { get; private set; } = "Pending";
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? DispatchedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    public void MarkDispatched() => (Status, DispatchedAt) = ("Dispatched", DateTimeOffset.UtcNow);
    public void MarkDone() => (Status, ProcessedAt) = ("Done", DateTimeOffset.UtcNow);
    public void MarkFailed(string error) { Status = "Failed"; Attempts++; LastError = error; }
}
