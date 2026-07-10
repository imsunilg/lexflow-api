namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to ops.task_dependencies (lexflow-database
/// Scripts/07_Ops/TaskDependencies). Composite-PK (task_id, depends_on_task_id), no
/// soft-delete columns — removing a dependency is a hard DELETE (mirrors the table's own
/// minimal shape: created_at/created_by only, per Build Playbook DB-7). task_id is blocked
/// (finish-to-start) until depends_on_task_id is Done; INSERT is guarded by a DB trigger
/// (ops.trg_task_dependencies_check_cycle) that raises CYCLE_DETECTED on a cyclic edge.
/// </summary>
public sealed class TaskDependency
{
    private TaskDependency()
    {
    }

    public TaskDependency(Guid tenantId, Guid taskId, Guid dependsOnTaskId)
    {
        TenantId = tenantId;
        TaskId = taskId;
        DependsOnTaskId = dependsOnTaskId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid DependsOnTaskId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
}
